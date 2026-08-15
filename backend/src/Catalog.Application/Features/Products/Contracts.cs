namespace Catalog.Application.Features.Products;

public sealed record CreateProductRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    [Required(ErrorMessage = "Price is required.")]
    [Range(0, 999_999.99, ErrorMessage = "Price must be between 0 and 999999.99.")]
    public required decimal Price { get; init; }
}

public sealed record UpdateProductRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    [Required(ErrorMessage = "Price is required.")]
    [Range(0, 999_999.99, ErrorMessage = "Price must be between 0 and 999999.99.")]
    public required decimal Price { get; init; }
}

public sealed record ProductListQuery
{
    [Range(1, int.MaxValue, ErrorMessage = "page must be 1 or greater.")]
    public int Page { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "pageSize must be between 1 and 100.")]
    public int PageSize { get; init; } = Paging.DefaultPageSize;

    public ProductSortField SortBy { get; init; } = ProductSortField.CreatedAt;

    public SortDirection Direction { get; init; } = SortDirection.Desc;

    public ProductListSpec ToSpec() =>
        new(Paging.ClampPage(Page), Paging.ClampPageSize(PageSize), SortBy, Direction);
}

public sealed record ProductSearchQuery
{
    [MaxLength(200)]
    public string? Name { get; init; }

    [Range(0, 999_999.99, ErrorMessage = "minPrice must be between 0 and 999999.99.")]
    public decimal? MinPrice { get; init; }

    [Range(0, 999_999.99, ErrorMessage = "maxPrice must be between 0 and 999999.99.")]
    public decimal? MaxPrice { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "page must be 1 or greater.")]
    public int Page { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "pageSize must be between 1 and 100.")]
    public int PageSize { get; init; } = Paging.DefaultPageSize;

    public ProductSortField SortBy { get; init; } = ProductSortField.CreatedAt;

    public SortDirection Direction { get; init; } = SortDirection.Desc;
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
