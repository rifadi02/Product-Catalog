namespace Catalog.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<Product?> GetForUpdateAsync(int id, CancellationToken ct = default) =>
        db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<ProductSnapshot?> GetSnapshotAsync(int id, CancellationToken ct = default)
    {
        return await db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductSnapshot(
                new ProductDto(p.Id, p.Name, p.Description, p.Price, p.CreatedAt, p.UpdatedAt),
                p.Version))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<ProductDto>> ListAsync(ProductListSpec spec, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking();
        return await PageAsync(query, spec.Page, spec.PageSize, spec.SortBy, spec.Direction, ct);
    }

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductSearchSpec spec, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking();

        if (spec.NameContains is { Length: > 0 } term)
        {
            var pattern = LikePattern.Contains(term);
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern, "\\"));
        }

        if (spec.MinPrice is { } min)
            query = query.Where(p => p.Price >= min);

        if (spec.MaxPrice is { } max)
            query = query.Where(p => p.Price <= max);

        return await PageAsync(query, spec.Page, spec.PageSize, spec.SortBy, spec.Direction, ct);
    }

    public void Add(Product product) => db.Products.Add(product);

    private static async Task<PagedResult<ProductDto>> PageAsync(
        IQueryable<Product> query,
        int page,
        int pageSize,
        ProductSortField sortBy,
        SortDirection direction,
        CancellationToken ct)
    {
        var total = await query.LongCountAsync(ct);

        var items = await ApplyOrdering(query, sortBy, direction)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProductMappings.Projection)
            .ToListAsync(ct);

        return new PagedResult<ProductDto>(items, page, pageSize, total);
    }

    /// <summary>
    /// §3.5 BR-3 / BR-4. The sort key arrives as an enum, so no user string ever reaches an
    /// ordering expression — an unknown value fails model binding with a 400 long before here.
    ///
    /// Every branch tie-breaks on <c>Id</c>. Without that, Postgres is free to return rows in a
    /// different order between page 1 and page 2 whenever the sort key repeats, and a user
    /// paging through sees duplicates and gaps.
    /// </summary>
    private static IQueryable<Product> ApplyOrdering(
        IQueryable<Product> query, ProductSortField sortBy, SortDirection direction)
    {
        var descending = direction is SortDirection.Desc;

        return (sortBy, descending) switch
        {
            (ProductSortField.Name, false) => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            (ProductSortField.Name, true) => query.OrderByDescending(p => p.Name).ThenBy(p => p.Id),

            (ProductSortField.Price, false) => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            (ProductSortField.Price, true) => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),

            (_, false) => query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            (_, true) => query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
        };
    }
}
