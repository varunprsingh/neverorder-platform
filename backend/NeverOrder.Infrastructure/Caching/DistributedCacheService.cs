using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using NeverOrder.Application.Abstractions;

namespace NeverOrder.Infrastructure.Caching;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Turns read-through caching off entirely, leaving every query to hit the database.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>A StackExchange.Redis connection string. Empty means an in-process cache instead.</summary>
    public string Redis { get; set; } = string.Empty;

    public string InstanceName { get; set; } = "neverorder:";

    public TimeSpan CatalogTimeToLive { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>A version token outlives the entries built from it, or they would survive it.</summary>
    public TimeSpan VersionTimeToLive { get; set; } = TimeSpan.FromDays(1);

    public bool UsesRedis => !string.IsNullOrWhiteSpace(Redis);
}

/// <summary>
/// Works against IDistributedCache so the same code runs on Redis or on an in-process cache. The
/// version token is a value rather than a counter precisely because IDistributedCache has no atomic
/// increment: two writers racing to invalidate both publish a fresh token, and either one is correct.
/// </summary>
public sealed class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly CacheOptions _options;
    private readonly ILogger<DistributedCacheService> _logger;

    public DistributedCacheService(
        IDistributedCache cache,
        CacheOptions options,
        ILogger<DistributedCacheService> logger)
    {
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            var payload = await _cache.GetStringAsync(key, cancellationToken);

            return payload is null ? null : JsonSerializer.Deserialize<T>(payload, CacheJson.Options);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A cache outage must degrade to a slower application, never a broken one.
            _logger.LogWarning(ex, "Cache read failed for {CacheKey}; falling back to the source", key);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where T : class
    {
        try
        {
            await _cache.SetStringAsync(
                key,
                JsonSerializer.Serialize(value, CacheJson.Options),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = timeToLive },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache write failed for {CacheKey}", key);
        }
    }

    public async Task<string> GetVersionAsync(string versionKey, CancellationToken cancellationToken = default)
    {
        var current = await GetRawAsync(versionKey, cancellationToken);
        if (current is not null)
        {
            return current;
        }

        var minted = NewToken();
        await SetRawAsync(versionKey, minted, cancellationToken);

        return minted;
    }

    public async Task InvalidateVersionAsync(string versionKey, CancellationToken cancellationToken = default)
    {
        await SetRawAsync(versionKey, NewToken(), cancellationToken);
        _logger.LogInformation("Invalidated cache version {VersionKey}", versionKey);
    }

    private async Task<string?> GetRawAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await _cache.GetStringAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache read failed for {CacheKey}", key);
            return null;
        }
    }

    private async Task SetRawAsync(string key, string value, CancellationToken cancellationToken)
    {
        try
        {
            await _cache.SetStringAsync(
                key,
                value,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _options.VersionTimeToLive
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache write failed for {CacheKey}", key);
        }
    }

    private static string NewToken() => Guid.NewGuid().ToString("n")[..12];
}

/// <summary>
/// Never bypassed when caching is off: callers still ask, and every answer is a miss with no write,
/// which keeps the read path identical in both modes.
/// </summary>
public sealed class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
        Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken = default)
        where T : class => Task.CompletedTask;

    public Task<string> GetVersionAsync(string versionKey, CancellationToken cancellationToken = default) =>
        Task.FromResult("off");

    public Task InvalidateVersionAsync(string versionKey, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal static class CacheJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
