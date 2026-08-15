namespace Catalog.Application.Common.Interfaces;

/// <summary>
/// Cache-aside over <c>IDistributedCache</c> (Redis in Compose, in-memory as fallback).
///
/// The version-counter pair exists because <c>IDistributedCache</c> has no wildcard delete and
/// <c>KEYS *</c> is not an option on a real Redis. Instead the list-cache key embeds a monotonic
/// version; bumping it on every write orphans every previous list key at once, and the orphans
/// age out on their own TTL.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) where T : class;

    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Current value of a monotonic counter. Returns 0 when the counter is absent.</summary>
    Task<long> GetVersionAsync(string key, CancellationToken ct = default);

    /// <summary>Advances a monotonic counter and returns the new value.</summary>
    Task<long> BumpVersionAsync(string key, CancellationToken ct = default);
}
