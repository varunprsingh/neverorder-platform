namespace NeverOrder.Application.Abstractions;

/// <summary>
/// A read-through cache with version-key invalidation. Rather than tracking and deleting every key
/// derived from the catalogue, each key embeds the current version token; publishing a new token
/// orphans the whole generation at once and lets it expire on its own.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Returns the current token, minting one if this is the first caller.</summary>
    Task<string> GetVersionAsync(string versionKey, CancellationToken cancellationToken = default);

    /// <summary>Publishes a new token, so every key built from the previous one stops being found.</summary>
    Task InvalidateVersionAsync(string versionKey, CancellationToken cancellationToken = default);
}

public static class CacheKeys
{
    public const string CatalogVersion = "catalog:version";

    public static string Products(string version, string query) => $"catalog:{version}:products:{query}";

    public static string Categories(string version) => $"catalog:{version}:categories";
}
