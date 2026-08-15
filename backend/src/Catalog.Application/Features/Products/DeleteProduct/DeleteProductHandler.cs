namespace Catalog.Application.Features.Products.DeleteProduct;

/// <summary>§3.10 — <c>DELETE /api/v1/products/{id}</c>. Soft delete, Admin only.</summary>
public sealed class DeleteProductHandler(
    IProductRepository products,
    IUnitOfWork uow,
    IClock clock,
    ICurrentUser currentUser,
    ICacheService cache,
    ILogger<DeleteProductHandler> logger)
{
    public async Task<Result> HandleAsync(int id, CancellationToken ct = default)
    {
        var product = await products.GetForUpdateAsync(id, ct);

        if (product is null)
            return Result.Failure(Error.NotFound("resource_not_found", $"Product {id} was not found."));

        product.SoftDelete(clock.UtcNow);
        await uow.SaveChangesAsync(ct);

        await cache.RemoveAsync(CacheKeys.Product(id), ct);
        await cache.BumpVersionAsync(CacheKeys.ProductsVersion, ct);

        logger.LogInformation("Product {ProductId} soft-deleted by {UserId}", id, currentUser.UserId);

        return Result.Success();
    }
}
