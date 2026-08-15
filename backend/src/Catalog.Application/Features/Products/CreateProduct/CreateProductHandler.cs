namespace Catalog.Application.Features.Products.CreateProduct;

/// <summary>§3.8 — <c>POST /api/v1/products</c>.</summary>
public sealed class CreateProductHandler(
    IProductRepository products,
    IUnitOfWork uow,
    IClock clock,
    ICurrentUser currentUser,
    ICacheService cache,
    ILogger<CreateProductHandler> logger)
{
    public async Task<Result<ProductDto>> HandleAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = Product.Create(
            request.Name,
            request.Description,
            request.Price,
            clock.UtcNow,
            currentUser.UserId);

        products.Add(product);
        await uow.SaveChangesAsync(ct);

        await cache.BumpVersionAsync(CacheKeys.ProductsVersion, ct);

        logger.LogInformation("Product {ProductId} created by {UserId}", product.Id, currentUser.UserId);

        return Result<ProductDto>.Success(product.ToDto());
    }
}
