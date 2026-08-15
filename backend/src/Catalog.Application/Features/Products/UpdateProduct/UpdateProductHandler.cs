namespace Catalog.Application.Features.Products.UpdateProduct;

/// <summary>§3.9 — <c>PUT /api/v1/products/{id}</c>. Full replacement of the mutable fields.</summary>
public sealed class UpdateProductHandler(
    IProductRepository products,
    IUnitOfWork uow,
    IClock clock,
    ICacheService cache,
    ILogger<UpdateProductHandler> logger)
{
    public async Task<Result> HandleAsync(
        int id,
        UpdateProductRequest request,
        string? ifMatch,
        CancellationToken ct = default)
    {
        var product = await products.GetForUpdateAsync(id, ct);

        if (product is null)
            return Result.Failure(Error.NotFound("resource_not_found", $"Product {id} was not found."));

        if (ifMatch is not null && !ETag.Matches(ifMatch, product.Version))
        {
            return Result.Failure(Error.Conflict("concurrency_conflict",
                "The resource was modified by another request."));
        }

        product.Update(request.Name, request.Description, request.Price, clock.UtcNow);

        await uow.SaveChangesAsync(ct);

        await cache.RemoveAsync(CacheKeys.Product(id), ct);
        await cache.BumpVersionAsync(CacheKeys.ProductsVersion, ct);

        logger.LogInformation("Product {ProductId} updated", id);

        return Result.Success();
    }
}
