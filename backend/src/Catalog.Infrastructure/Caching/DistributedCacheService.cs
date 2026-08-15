namespace Catalog.Infrastructure.Caching;

/// <summary>
/// Cache-aside over <see cref="IDistributedCache"/>. Redis in Compose, in-memory when Redis is
/// absent — the swap happens in composition (see <c>DependencyInjection</c>), not here, so the
/// tests never need a Redis container.
///
/// <para><b>A cache failure is never a request failure.</b> Every operation swallows its
/// exception and logs it. A Redis blip must degrade the API to "slower", not to "down" — which
/// is also why the Redis health check is tagged <c>Degraded</c> rather than unhealthy (§3.12).</para>
/// </summary>
public sealed class DistributedCacheService(
    IDistributedCache cache,
    IClock clock,
    ILogger<DistributedCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        try
        {
            var payload = await cache.GetStringAsync(key, ct);
            return payload is null ? null : JsonSerializer.Deserialize<T>(payload, SerializerOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache read failed for {CacheKey}; falling through to the database", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await cache.SetStringAsync(key, payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache write failed for {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache eviction failed for {CacheKey}", key);
        }
    }

    public async Task<long> GetVersionAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var raw = await cache.GetStringAsync(key, ct);
            return long.TryParse(raw, out var version) ? version : 0L;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache version read failed for {CacheKey}", key);
            return 0L;
        }
    }

    /// <summary>
    /// Advances the list-cache generation. Bumping this orphans every previous
    /// <c>products:v{n}:page:…</c> key at once, which is how invalidation happens without a
    /// wildcard delete — <c>IDistributedCache</c> has none, and <c>KEYS *</c> on a real Redis is
    /// not an option.
    ///
    /// <para>The new value is <c>max(current + 1, ticks)</c> rather than a plain increment.
    /// Read-modify-write across N API replicas can lose an update; anchoring to a monotonically
    /// increasing clock means the version still moves forward even when it does, so a lost
    /// update can never resurrect a stale key.</para>
    /// </summary>
    public async Task<long> BumpVersionAsync(string key, CancellationToken ct = default)
    {
        var next = clock.UtcNow.Ticks;

        try
        {
            var current = await GetVersionAsync(key, ct);
            next = Math.Max(current + 1, next);

            await cache.SetStringAsync(key, next.ToString(),
                new DistributedCacheEntryOptions(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Cache version bump failed for {CacheKey}", key);
        }

        return next;
    }
}
