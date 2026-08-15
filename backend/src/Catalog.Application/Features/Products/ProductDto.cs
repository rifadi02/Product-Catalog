namespace Catalog.Application.Features.Products;

public sealed record ProductDto(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record ProductSnapshot(ProductDto Dto, uint Version)
{
    public string ETag => Common.ETag.From(Version);
}

public static class ProductMappings
{
    public static ProductDto ToDto(this Product p) =>
        new(p.Id, p.Name, p.Description, p.Price, p.CreatedAt, p.UpdatedAt);

    public static readonly Expression<Func<Product, ProductDto>> Projection =
        p => new ProductDto(p.Id, p.Name, p.Description, p.Price, p.CreatedAt, p.UpdatedAt);
}

public sealed record ProductListSpec(
    int Page,
    int PageSize,
    ProductSortField SortBy,
    SortDirection Direction);

public sealed record ProductSearchSpec(
    int Page,
    int PageSize,
    ProductSortField SortBy,
    SortDirection Direction,
    string? NameContains,
    decimal? MinPrice,
    decimal? MaxPrice);

public static class Paging
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;
    public static int ClampPageSize(int pageSize) => Math.Clamp(pageSize, 1, MaxPageSize);
    public static int ClampPage(int page) => page < 1 ? 1 : page;
}
