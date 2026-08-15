namespace Catalog.Application.Features.Products.SearchProducts;

/// <summary>
/// §3.6 — <c>GET /api/v1/products/search</c>.
///
/// Deliberately <b>not</b> cached: <c>name</c> is free text, so the key space is unbounded and a
/// naive cache-aside here fills Redis with single-use keys and evicts the entries that help.
/// </summary>
public sealed class SearchProductsHandler(IProductRepository products)
{
    public async Task<Result<PagedResult<ProductDto>>> HandleAsync(
        ProductSearchQuery query, CancellationToken ct = default)
    {
        var range = PriceRange.Create(query.MinPrice, query.MaxPrice);
        if (!range.IsSuccess)
            return Result<PagedResult<ProductDto>>.Failure(range.Error!);

        var name = string.IsNullOrWhiteSpace(query.Name) ? null : query.Name.Trim();

        var spec = new ProductSearchSpec(
            Paging.ClampPage(query.Page),
            Paging.ClampPageSize(query.PageSize),
            query.SortBy,
            query.Direction,
            name,
            range.Value.Min,
            range.Value.Max);

        return Result<PagedResult<ProductDto>>.Success(await products.SearchAsync(spec, ct));
    }
}
