namespace Catalog.Application.Features.Products.GetProductById;

/// <summary>§3.7 — <c>GET /api/v1/products/{id}</c>. Cache-aside, TTL 5 min.</summary>
public sealed class GetProductByIdHandler(
    IProductRepository products,
    ICacheService cache,
    ILogger<GetProductByIdHandler> logger)
{
    public async Task<Result<ProductSnapshot>> HandleAsync(int id, CancellationToken ct = default)
    {
        var key = CacheKeys.Product(id);

        var cached = await cache.GetAsync<ProductSnapshot>(key, ct);
        if (cached is not null)
        {
            logger.LogDebug("Cache hit for {CacheKey}", key);
            return Result<ProductSnapshot>.Success(cached);
        }

        var snapshot = await products.GetSnapshotAsync(id, ct);

        if (snapshot is null)
        {
            logger.LogDebug("Cache miss for {CacheKey}; product does not exist", key);
            return Result<ProductSnapshot>.Failure(
                Error.NotFound("resource_not_found", $"Product {id} was not found."));
        }

        logger.LogDebug("Cache miss for {CacheKey}; populating", key);
        await cache.SetAsync(key, snapshot, CacheKeys.DefaultTtl, ct);

        return Result<ProductSnapshot>.Success(snapshot);
    }
}
