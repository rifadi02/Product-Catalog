namespace Catalog.Application.Common.Caching;

/// <summary>
/// Every cache key in the application is built here. Keys assembled inline at call sites drift,
/// and a drifted key is a cache that silently never hits.
/// </summary>
public static class CacheKeys
{
    /// <summary>Monotonic counter bumped on every product write; embedded in list keys.</summary>
    public const string ProductsVersion = "products:version";

    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    /// <summary> only the first three pages are cached. Deep pages are rare, and
    /// caching them unbounded is how a cache turns into a memory leak.</summary>
    public const int MaxCachedPage = 3;

    public static string Product(int id) => $"product:{id}";

    public static string ProductsPage(long version, int page, int pageSize,
        ProductSortField sortBy, SortDirection direction)
        => $"products:v{version}:page:{page}:size:{pageSize}:sort:{sortBy}:{direction}";

    public static bool IsPageCacheable(int page) => page is >= 1 and <= MaxCachedPage;
}
