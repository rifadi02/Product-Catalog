namespace Catalog.IntegrationTests.Fixtures;

/// <summary>
/// A real <see cref="IDistributedCache"/> that can be emptied between tests.
///
/// <para>The API is started once per collection, so its cache singleton outlives any individual
/// test while <c>ResetDatabaseAsync</c> wipes the database underneath it. Worse,
/// <c>TRUNCATE … RESTART IDENTITY</c> hands the next test the same product ids, so a cache entry
/// left behind by the previous test is not merely stale — it is a plausible-looking answer for a
/// row that no longer exists. That is how a passing test starts asserting against another test's
/// data.</para>
///
/// <para>The delegate is a genuine <see cref="MemoryDistributedCache"/>, the same implementation
/// the application falls back to when Redis is absent, so the cache-aside logic under test is not
/// altered — only the ability to drop the whole store is added. <see cref="IDistributedCache"/>
/// has no <c>Clear</c>, hence the swap.</para>
/// </summary>
internal sealed class ResettableDistributedCache : IDistributedCache
{
    private IDistributedCache _inner = Create();

    private static IDistributedCache Create() =>
        new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()),
            NullLoggerFactory.Instance);

    /// <summary>Drops every entry. Tests in a collection run sequentially, so no synchronisation.</summary>
    public void Clear() => _inner = Create();

    public byte[]? Get(string key) => _inner.Get(key);

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        _inner.GetAsync(key, token);

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        _inner.Set(key, value, options);

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default) => _inner.SetAsync(key, value, options, token);

    public void Refresh(string key) => _inner.Refresh(key);

    public Task RefreshAsync(string key, CancellationToken token = default) =>
        _inner.RefreshAsync(key, token);

    public void Remove(string key) => _inner.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default) =>
        _inner.RemoveAsync(key, token);
}

/// <summary>
/// Gives each test client its own apparent source address, so the rate limiter partitions them
/// apart.
///
/// <para>The limiter keys on <c>RemoteIpAddress</c>, which <c>TestServer</c> leaves null — every
/// request in the suite therefore lands in one partition named "unknown". Registration allows five
/// attempts per fifteen minutes, so the sixth test to register anything got a 429 and the rest of
/// the run collapsed. The limiter is a singleton with no reset hook, and its window is far longer
/// than the suite takes to run, so isolation has to come from the partition key.</para>
///
/// <para>This lives in the test project and is injected through <c>ConfigureTestServices</c>: the
/// production pipeline gains nothing and has no test-only header to honour. Tests that want to
/// *exercise* the limit ask for a shared identity instead — see
/// <c>CatalogApiFactory.CreateClientAs</c>.</para>
/// </summary>
internal sealed class TestClientAddressStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Client";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            var id = context.Request.Headers[HeaderName].ToString();

            if (!string.IsNullOrWhiteSpace(id))
                context.Connection.RemoteIpAddress = SyntheticAddress(id);

            await nextMiddleware();
        });

        next(app);
    };

    /// <summary>
    /// A stable IPv6 address derived from the client id. SHA-256 truncated to sixteen bytes is
    /// exactly the width of an IPv6 address, which makes a collision between two ids about as
    /// likely as a GUID collision — and unlike a 32-bit IPv4 mapping, it will not silently merge
    /// two tests into one rate-limit partition.
    /// </summary>
    private static IPAddress SyntheticAddress(string clientId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(clientId)).AsSpan(0, 16));
}
