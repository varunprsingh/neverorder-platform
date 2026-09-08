using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace NeverOrder.Infrastructure.Caching;

/// <summary>
/// Registered only when a real cache server is configured. Writes and reads a probe key, because a
/// server that accepts connections but refuses writes is still not usable.
/// </summary>
public sealed class DistributedCacheHealthCheck : IHealthCheck
{
    private const string ProbeKey = "health:probe";

    private readonly IDistributedCache _cache;

    public DistributedCacheHealthCheck(IDistributedCache cache) => _cache = cache;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var token = Guid.NewGuid().ToString("n");

            await _cache.SetStringAsync(
                ProbeKey,
                token,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) },
                cancellationToken);

            var roundTripped = await _cache.GetStringAsync(ProbeKey, cancellationToken);

            return roundTripped == token
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, "The probe key did not round-trip.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "The cache is unreachable.", ex);
        }
    }
}
