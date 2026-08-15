namespace Catalog.Application.Features.Products.ListProducts;

/// <summary>§3.5 — <c>GET /api/v1/products</c>. Cache-aside on pages 1–3, TTL 5 min.</summary>
public sealed class ListProductsHandler(
    IProductRepository products,
    ICacheService cache,
    ILogger<ListProductsHandler> logger)
{
    public async Task<Result<PagedResult<ProductDto>>> HandleAsync(
        ProductListQuery query, CancellationToken ct = default)
    {
        var spec = query.ToSpec();

        if (!CacheKeys.IsPageCacheable(spec.Page))
            return Result<PagedResult<ProductDto>>.Success(await products.ListAsync(spec, ct));

        var version = await cache.GetVersionAsync(CacheKeys.ProductsVersion, ct);
        var key = CacheKeys.ProductsPage(version, spec.Page, spec.PageSize, spec.SortBy, spec.Direction);

        var cached = await cache.GetAsync<PagedResult<ProductDto>>(key, ct);
        if (cached is not null)
        {
            logger.LogDebug("Cache hit for {CacheKey}", key);
            return Result<PagedResult<ProductDto>>.Success(cached);
        }

        logger.LogDebug("Cache miss for {CacheKey}; populating", key);
        var page = await products.ListAsync(spec, ct);
        await cache.SetAsync(key, page, CacheKeys.DefaultTtl, ct);

        return Result<PagedResult<ProductDto>>.Success(page);
    }
}
