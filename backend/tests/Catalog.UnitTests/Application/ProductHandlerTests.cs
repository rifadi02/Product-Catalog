namespace Catalog.UnitTests.Application;

public sealed class ProductHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Caller = Guid.CreateVersion7();

    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public ProductHandlerTests()
    {
        _clock.UtcNow.Returns(Now);
        _currentUser.UserId.Returns(Caller);
    }

    private static Product Existing(string name = "Lamp", decimal price = 10m) =>
        Product.Create(name, "desc", price, Now.AddDays(-1), Caller);

    [Fact]
    public async Task Create_attributes_the_product_to_the_caller_and_bumps_the_list_generation()
    {
        var handler = new CreateProductHandler(_products, _uow, _clock, _currentUser, _cache,
            NullLogger<CreateProductHandler>.Instance);

        Product? added = null;
        _products.When(p => p.Add(Arg.Any<Product>())).Do(c => added = c.Arg<Product>());

        var result = await handler.HandleAsync(new CreateProductRequest
        {
            Name = "  Desk Lamp  ",
            Description = "  ",
            Price = 25.499m
        });

        result.IsSuccess.Should().BeTrue();

        added!.Name.Should().Be("Desk Lamp");
        added.Description.Should().BeNull();
        added.Price.Should().Be(25.50m);
        added.CreatedAt.Should().Be(Now);
        added.CreatedBy.Should().Be(Caller, "attribution comes from the token, never from the body");
        added.UpdatedAt.Should().BeNull();

        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).BumpVersionAsync(CacheKeys.ProductsVersion, Arg.Any<CancellationToken>());
    }

    private UpdateProductHandler UpdateHandler() =>
        new(_products, _uow, _clock, _cache, NullLogger<UpdateProductHandler>.Instance);

    private static UpdateProductRequest UpdateRequest() =>
        new() { Name = "Renamed", Description = null, Price = 99m };

    [Fact]
    public async Task Update_returns_404_when_the_product_is_absent_or_soft_deleted()
    {
        _products.GetForUpdateAsync(7).Returns((Product?)null);

        var result = await UpdateHandler().HandleAsync(7, UpdateRequest(), ifMatch: null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be("resource_not_found");
    }

    [Fact]
    public async Task Update_without_If_Match_is_last_write_wins()
    {
        var product = Existing();
        _products.GetForUpdateAsync(7).Returns(product);

        var result = await UpdateHandler().HandleAsync(7, UpdateRequest(), ifMatch: null);

        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("Renamed");
        product.Description.Should().BeNull("PUT is a replacement, not a patch");
        product.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Update_with_a_stale_If_Match_is_a_conflict_and_writes_nothing()
    {
        var product = Existing();
        _products.GetForUpdateAsync(7).Returns(product);

        var result = await UpdateHandler().HandleAsync(7, UpdateRequest(), ifMatch: "\"999\"");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("concurrency_conflict");

        product.Name.Should().Be("Lamp", "a rejected precondition must leave the entity untouched");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_with_a_matching_If_Match_proceeds()
    {
        var product = Existing();
        _products.GetForUpdateAsync(7).Returns(product);

        var result = await UpdateHandler().HandleAsync(7, UpdateRequest(), ifMatch: ETag.From(product.Version));

        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task Update_evicts_the_entity_key_and_bumps_the_list_generation()
    {
        _products.GetForUpdateAsync(7).Returns(Existing());

        await UpdateHandler().HandleAsync(7, UpdateRequest(), ifMatch: null);

        await _cache.Received(1).RemoveAsync(CacheKeys.Product(7), Arg.Any<CancellationToken>());
        await _cache.Received(1).BumpVersionAsync(CacheKeys.ProductsVersion, Arg.Any<CancellationToken>());
    }

    private DeleteProductHandler DeleteHandler() =>
        new(_products, _uow, _clock, _currentUser, _cache, NullLogger<DeleteProductHandler>.Instance);

    [Fact]
    public async Task Delete_soft_deletes_and_invalidates()
    {
        var product = Existing();
        _products.GetForUpdateAsync(7).Returns(product);

        var result = await DeleteHandler().HandleAsync(7);

        result.IsSuccess.Should().BeTrue();
        product.IsDeleted.Should().BeTrue();
        product.DeletedAt.Should().Be(Now);

        await _cache.Received(1).RemoveAsync(CacheKeys.Product(7), Arg.Any<CancellationToken>());
        await _cache.Received(1).BumpVersionAsync(CacheKeys.ProductsVersion, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deleting_an_already_deleted_product_is_a_404_not_a_204()
    {
        _products.GetForUpdateAsync(7).Returns((Product?)null);

        var result = await DeleteHandler().HandleAsync(7);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
    }

    private GetProductByIdHandler GetHandler() =>
        new(_products, _cache, NullLogger<GetProductByIdHandler>.Instance);

    private static ProductSnapshot Snapshot(int id = 7, uint version = 42) =>
        new(new ProductDto(id, "Lamp", "desc", 10m, Now, null), version);

    [Fact]
    public async Task A_cache_hit_never_touches_the_database()
    {
        _cache.GetAsync<ProductSnapshot>(CacheKeys.Product(7)).Returns(Snapshot());

        var result = await GetHandler().HandleAsync(7);

        result.IsSuccess.Should().BeTrue();
        await _products.DidNotReceive().GetSnapshotAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_cache_miss_queries_and_then_populates()
    {
        _cache.GetAsync<ProductSnapshot>(CacheKeys.Product(7)).Returns((ProductSnapshot?)null);
        _products.GetSnapshotAsync(7).Returns(Snapshot());

        var result = await GetHandler().HandleAsync(7);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ETag.Should().Be("\"42\"");

        await _cache.Received(1).SetAsync(
            CacheKeys.Product(7), Arg.Any<ProductSnapshot>(), CacheKeys.DefaultTtl,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_404_is_never_cached()
    {
        _cache.GetAsync<ProductSnapshot>(CacheKeys.Product(999)).Returns((ProductSnapshot?)null);
        _products.GetSnapshotAsync(999).Returns((ProductSnapshot?)null);

        var result = await GetHandler().HandleAsync(999);

        result.IsSuccess.Should().BeFalse();
        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(), Arg.Any<ProductSnapshot>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    private ListProductsHandler ListHandler() =>
        new(_products, _cache, NullLogger<ListProductsHandler>.Instance);

    [Fact]
    public async Task Deep_pages_are_not_cached()
    {
        _products.ListAsync(Arg.Any<ProductListSpec>()).Returns(PagedResult<ProductDto>.Empty(9, 20));

        var result = await ListHandler().HandleAsync(new ProductListQuery { Page = 9 });

        result.IsSuccess.Should().BeTrue();
        await _cache.DidNotReceive().GetAsync<PagedResult<ProductDto>>(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_list_cache_key_embeds_the_current_generation()
    {
        _cache.GetVersionAsync(CacheKeys.ProductsVersion).Returns(17L);
        _products.ListAsync(Arg.Any<ProductListSpec>()).Returns(PagedResult<ProductDto>.Empty(1, 20));

        await ListHandler().HandleAsync(new ProductListQuery());

        var expected = CacheKeys.ProductsPage(17, 1, 20, ProductSortField.CreatedAt, SortDirection.Desc);
        await _cache.Received(1).GetAsync<PagedResult<ProductDto>>(expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_oversized_pageSize_reaches_the_repository_clamped()
    {
        _products.ListAsync(Arg.Any<ProductListSpec>()).Returns(PagedResult<ProductDto>.Empty(1, 100));

        await ListHandler().HandleAsync(new ProductListQuery { Page = 4, PageSize = 10_000 });

        await _products.Received(1).ListAsync(
            Arg.Is<ProductListSpec>(s => s.PageSize == 100), Arg.Any<CancellationToken>());
    }

    private SearchProductsHandler SearchHandler() => new(_products);

    [Fact]
    public async Task Search_rejects_an_inverted_price_range_against_the_minPrice_field()
    {
        var result = await SearchHandler().HandleAsync(
            new ProductSearchQuery { MinPrice = 500m, MaxPrice = 100m });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Fields.Should().ContainKey("minPrice");
    }

    [Fact]
    public async Task A_whitespace_only_name_is_treated_as_no_filter()
    {
        _products.SearchAsync(Arg.Any<ProductSearchSpec>()).Returns(PagedResult<ProductDto>.Empty(1, 20));

        await SearchHandler().HandleAsync(new ProductSearchQuery { Name = "   " });

        await _products.Received(1).SearchAsync(
            Arg.Is<ProductSearchSpec>(s => s.NameContains == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_name_filter_is_trimmed_before_it_reaches_the_repository()
    {
        _products.SearchAsync(Arg.Any<ProductSearchSpec>()).Returns(PagedResult<ProductDto>.Empty(1, 20));

        await SearchHandler().HandleAsync(new ProductSearchQuery { Name = "  lamp  " });

        await _products.Received(1).SearchAsync(
            Arg.Is<ProductSearchSpec>(s => s.NameContains == "lamp"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_is_never_cached()
    {
        _products.SearchAsync(Arg.Any<ProductSearchSpec>()).Returns(PagedResult<ProductDto>.Empty(1, 20));

        await SearchHandler().HandleAsync(new ProductSearchQuery { Name = "lamp" });

        await _cache.DidNotReceiveWithAnyArgs().GetAsync<PagedResult<ProductDto>>(default!);
        await _cache.DidNotReceiveWithAnyArgs().SetAsync(default!, default(PagedResult<ProductDto>)!, default);
    }
}
