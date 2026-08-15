namespace Catalog.UnitTests.Api;

/// <summary>
/// The controller's own responsibilities: conditional-request handling, header plumbing, and the
/// shape of what comes back. Business rules live in the handler tests.
/// </summary>
public sealed class ProductsControllerTests
{
    private static readonly Guid Caller = Guid.CreateVersion7();

    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly ProductsController _controller = new();

    public ProductsControllerTests()
    {
        _clock.UtcNow.Returns(ControllerTestContext.Now);
        _currentUser.UserId.Returns(Caller);
    }

    private GetProductByIdHandler GetHandler() =>
        new(_products, _cache, NullLogger<GetProductByIdHandler>.Instance);

    private ListProductsHandler ListHandler() =>
        new(_products, _cache, NullLogger<ListProductsHandler>.Instance);

    private CreateProductHandler CreateHandler() =>
        new(_products, _uow, _clock, _currentUser, _cache, NullLogger<CreateProductHandler>.Instance);

    private UpdateProductHandler UpdateHandler() =>
        new(_products, _uow, _clock, _cache, NullLogger<UpdateProductHandler>.Instance);

    private DeleteProductHandler DeleteHandler() =>
        new(_products, _uow, _clock, _currentUser, _cache, NullLogger<DeleteProductHandler>.Instance);

    // ── GET /products/{id} ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_returns_the_product_and_advertises_its_ETag()
    {
        _controller.WithHttpContext();
        _products.GetSnapshotAsync(1).Returns(new ProductSnapshot(ControllerTestContext.Dto(), 42));

        var result = await _controller.GetById(1, GetHandler(), default);

        result.Should().BeOfType<OkObjectResult>()
              .Which.Value.Should().BeOfType<ProductDto>()
              .Which.Id.Should().Be(1);

        _controller.Response.Headers.ETag.ToString().Should().Be("\"42\"",
            "a client cannot make a conditional request for an ETag it was never given");
    }

    [Fact]
    public async Task GetById_returns_304_when_If_None_Match_is_current()
    {
        var http = _controller.WithHttpContext();
        http.Request.Headers.IfNoneMatch = "\"42\"";

        _products.GetSnapshotAsync(1).Returns(new ProductSnapshot(ControllerTestContext.Dto(), 42));

        var result = await _controller.GetById(1, GetHandler(), default);

        result.Should().BeOfType<StatusCodeResult>()
              .Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);

        _controller.Response.Headers.ETag.ToString().Should().Be("\"42\"",
            "RFC 9110 requires the ETag on a 304 as well, or the client cannot revalidate again");
    }

    [Fact]
    public async Task GetById_returns_the_body_when_If_None_Match_is_stale()
    {
        var http = _controller.WithHttpContext();
        http.Request.Headers.IfNoneMatch = "\"41\"";

        _products.GetSnapshotAsync(1).Returns(new ProductSnapshot(ControllerTestContext.Dto(), 42));

        var result = await _controller.GetById(1, GetHandler(), default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_maps_a_missing_product_to_a_404_problem_document()
    {
        _controller.WithHttpContext();
        _products.GetSnapshotAsync(9).Returns((ProductSnapshot?)null);

        var result = await _controller.GetById(9, GetHandler(), default);

        ControllerTestContext.StatusOf(result).Should().Be(StatusCodes.Status404NotFound);
        ControllerTestContext.CodeOf(result).Should().Be("resource_not_found");

        var problem = ControllerTestContext.ProblemFrom(result);
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // ── POST /products ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_returns_201_with_a_Location_pointing_at_the_new_product()
    {
        _controller.WithHttpContext();

        var result = await _controller.Create(
            new CreateProductRequest { Name = "Desk Lamp", Description = null, Price = 24.99m },
            CreateHandler(), default);

        var created = result.Should().BeOfType<CreatedResult>().Subject;

        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Location.Should().StartWith("/api/v1/products/");
        created.Value.Should().BeOfType<ProductDto>();
    }

    // ── PUT /products/{id} ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_forwards_the_If_Match_header_to_the_handler()
    {
        var http = _controller.WithHttpContext();
        http.Request.Headers.IfMatch = "\"7\"";

        var product = Product.Create("Lamp", "d", 10m, ControllerTestContext.Now, Caller);
        _products.GetForUpdateAsync(1).Returns(product);

        var result = await _controller.Update(
            1, new UpdateProductRequest { Name = "Renamed", Description = null, Price = 5m },
            UpdateHandler(), default);

        // Version is 0 on an unsaved entity, so If-Match: "7" is genuinely stale and the handler
        // must reject it. The assertion is really about the header travelling at all: a controller
        // that failed to read If-Match would have skipped the check and returned a silent
        // last-write-wins 204, quietly discarding somebody else's edit.
        ControllerTestContext.StatusOf(result).Should().Be(StatusCodes.Status409Conflict);
        ControllerTestContext.CodeOf(result).Should().Be("concurrency_conflict");
    }

    [Fact]
    public async Task Update_treats_a_blank_If_Match_as_absent_and_succeeds()
    {
        var http = _controller.WithHttpContext();
        http.Request.Headers.IfMatch = "   ";

        _products.GetForUpdateAsync(1).Returns(
            Product.Create("Lamp", "d", 10m, ControllerTestContext.Now, Caller));

        var result = await _controller.Update(
            1, new UpdateProductRequest { Name = "Renamed", Description = null, Price = 5m },
            UpdateHandler(), default);

        result.Should().BeOfType<NoContentResult>(
            "whitespace is not a precondition; treating it as one would reject a valid request");
    }

    [Fact]
    public async Task Update_returns_204_with_no_body()
    {
        _controller.WithHttpContext();

        _products.GetForUpdateAsync(1).Returns(
            Product.Create("Lamp", "d", 10m, ControllerTestContext.Now, Caller));

        var result = await _controller.Update(
            1, new UpdateProductRequest { Name = "Renamed", Description = null, Price = 5m },
            UpdateHandler(), default);

        result.Should().BeOfType<NoContentResult>();
    }

    // ── DELETE /products/{id} ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_returns_204_on_success()
    {
        _controller.WithHttpContext();

        _products.GetForUpdateAsync(1).Returns(
            Product.Create("Lamp", "d", 10m, ControllerTestContext.Now, Caller));

        var result = await _controller.Delete(1, DeleteHandler(), default);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_maps_a_missing_product_to_a_404_problem_document()
    {
        _controller.WithHttpContext();
        _products.GetForUpdateAsync(9).Returns((Product?)null);

        var result = await _controller.Delete(9, DeleteHandler(), default);

        ControllerTestContext.StatusOf(result).Should().Be(StatusCodes.Status404NotFound);
        ControllerTestContext.CodeOf(result).Should().Be("resource_not_found");
    }

    // ── GET /products ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_projects_the_paged_result_onto_the_wire_contract()
    {
        _controller.WithHttpContext();

        _products.ListAsync(Arg.Any<ProductListSpec>())
                 .Returns(new PagedResult<ProductDto>([ControllerTestContext.Dto()], 2, 20, 45));

        var result = await _controller.List(new ProductListQuery { Page = 2 }, ListHandler(), default);

        var page = result.Should().BeOfType<OkObjectResult>()
                         .Which.Value.Should().BeOfType<PagedResponse<ProductDto>>().Subject;

        page.Page.Should().Be(2);
        page.PageSize.Should().Be(20);
        page.TotalCount.Should().Be(45);
        page.TotalPages.Should().Be(3);
        page.HasNextPage.Should().BeTrue();
        page.HasPreviousPage.Should().BeTrue();
        page.Items.Should().ContainSingle();
    }
}
