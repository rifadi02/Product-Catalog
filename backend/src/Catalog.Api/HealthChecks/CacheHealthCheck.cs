namespace Catalog.Api.HealthChecks;

/// <summary>
/// Readiness probe for the cache tier.
///
/// <para>It goes through <see cref="IDistributedCache"/> — the same abstraction the application
/// uses — rather than opening its own Redis connection. Two reasons. First, it reports on the
/// backend composition actually selected instead of the one configuration was read for, so it
/// cannot disagree with reality. Second, when Redis is absent the cache is genuinely healthy: it
/// is an in-process cache and there is nothing to be unreachable. A probe that hard-failed on a
/// blank <c>Redis:Connection</c> would report a working service as broken.</para>
///
/// <para>A Redis failure is <see cref="HealthStatus.Degraded"/>, never unhealthy — cache misses
/// fall through to the database, so the API is slower, not down (§3.12). Deliberately not tagged
/// so a load balancer draining on readiness will not pull a serving instance out for a cache
/// blip.</para>
/// </summary>
public sealed class CacheHealthCheck(
    IDistributedCache cache, CacheModeReport mode) : IHealthCheck
{
    private const string ProbeKey = "catalog:health:probe";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object> { ["mode"] = mode.Mode };

        if (mode.Mode == "in-memory")
            return HealthCheckResult.Healthy("Cache is in-process; no remote dependency.", data);

        try
        {
            // A round trip, not just a connect: a Redis that accepts connections but rejects
            // writes (out of memory, read-only replica) is not a usable cache.
            await cache.SetStringAsync(
                ProbeKey,
                "ok",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
                cancellationToken);

            var value = await cache.GetStringAsync(ProbeKey, cancellationToken);

            return value == "ok"
                ? HealthCheckResult.Healthy("Redis round trip succeeded.", data)
                : new HealthCheckResult(context.Registration.FailureStatus,
                    "Redis accepted the write but did not return the value.", data: data);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus,
                "Redis is unreachable; the API is serving from the database.", ex, data);
        }
    }
}
