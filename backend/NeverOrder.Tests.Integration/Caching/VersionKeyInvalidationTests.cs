using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeverOrder.Application.Abstractions;
using NeverOrder.Infrastructure.Caching;

namespace NeverOrder.Tests.Integration.Caching;

public sealed class VersionKeyInvalidationTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly IDistributedCache _backing = new MemoryDistributedCache(
        Options.Create(new MemoryDistributedCacheOptions()));

    private readonly DistributedCacheService _cache;

    public VersionKeyInvalidationTests() =>
        _cache = new DistributedCacheService(
            _backing,
            new CacheOptions(),
            NullLogger<DistributedCacheService>.Instance);

    [Fact]
    public async Task A_version_token_is_minted_once_and_then_reused()
    {
        var first = await _cache.GetVersionAsync(CacheKeys.CatalogVersion);
        var second = await _cache.GetVersionAsync(CacheKeys.CatalogVersion);

        first.Should().NotBeNullOrWhiteSpace();
        second.Should().Be(first);
    }

    [Fact]
    public async Task Invalidating_orphans_every_key_built_from_the_old_token()
    {
        var oldVersion = await _cache.GetVersionAsync(CacheKeys.CatalogVersion);
        var oldKey = CacheKeys.Categories(oldVersion);

        await _cache.SetAsync(oldKey, new List<string> { "food" }, Ttl);
        (await _cache.GetAsync<List<string>>(oldKey)).Should().NotBeNull();

        await _cache.InvalidateVersionAsync(CacheKeys.CatalogVersion);

        var newVersion = await _cache.GetVersionAsync(CacheKeys.CatalogVersion);
        newVersion.Should().NotBe(oldVersion);

        // The stale entry is never deleted, only unreachable, which is the point: invalidation costs
        // one write no matter how many keys the old generation had.
        (await _cache.GetAsync<List<string>>(CacheKeys.Categories(newVersion))).Should().BeNull();
        (await _cache.GetAsync<List<string>>(oldKey)).Should().NotBeNull();
    }

    [Fact]
    public async Task A_cache_that_throws_degrades_to_a_miss_instead_of_failing_the_request()
    {
        var cache = new DistributedCacheService(
            new ThrowingCache(),
            new CacheOptions(),
            NullLogger<DistributedCacheService>.Instance);

        var act = async () => await cache.GetAsync<List<string>>("any-key");

        await act.Should().NotThrowAsync();
        (await cache.GetAsync<List<string>>("any-key")).Should().BeNull();
    }

    private sealed class ThrowingCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Cache is down.");

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Cache is down.");

        public void Refresh(string key) => throw new InvalidOperationException("Cache is down.");

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Cache is down.");

        public void Remove(string key) => throw new InvalidOperationException("Cache is down.");

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            throw new InvalidOperationException("Cache is down.");

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new InvalidOperationException("Cache is down.");

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default) =>
            throw new InvalidOperationException("Cache is down.");
    }
}
