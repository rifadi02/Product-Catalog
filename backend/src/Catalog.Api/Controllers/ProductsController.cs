namespace Catalog.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/products")]
[Produces("application/json")]
public sealed class ProductsController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [Authorize(Policy = Policies.CanReadProducts)]
    [ProducesResponseType<PagedResponse<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] ProductListQuery query,
        [FromServices] ListProductsHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(query, ct);

        return result.IsSuccess
            ? Ok(ToResponse(result.Value!))
            : result.Error!.ToProblem(HttpContext);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    [Authorize(Policy = Policies.CanReadProducts)]
    [ProducesResponseType<PagedResponse<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] ProductSearchQuery query,
        [FromServices] SearchProductsHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(query, ct);

        return result.IsSuccess
            ? Ok(ToResponse(result.Value!))
            : result.Error!.ToProblem(HttpContext);
    }

    [HttpGet("{id:int}", Name = nameof(GetById))]
    [AllowAnonymous]
    [Authorize(Policy = Policies.CanReadProducts)]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute][Range(1, int.MaxValue, ErrorMessage = "id must be 1 or greater.")] int id,
        [FromServices] GetProductByIdHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, ct);

        if (!result.IsSuccess)
            return result.Error!.ToProblem(HttpContext);

        var snapshot = result.Value!;
        Response.Headers.ETag = snapshot.ETag;

        var ifNoneMatch = Request.Headers[HeaderNames.IfNoneMatch].ToString();
        if (ETag.Matches(ifNoneMatch, snapshot.Version))
            return StatusCode(StatusCodes.Status304NotModified);

        return Ok(snapshot.Dto);
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanWriteProducts)]
    [ProducesResponseType<ProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        [FromServices] CreateProductHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);

        if (!result.IsSuccess)
            return result.Error!.ToProblem(HttpContext);

        var product = result.Value!;

        return Created($"/api/v1/products/{product.Id}", product);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.CanWriteProducts)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute][Range(1, int.MaxValue, ErrorMessage = "id must be 1 or greater.")] int id,
        [FromBody] UpdateProductRequest request,
        [FromServices] UpdateProductHandler handler,
        CancellationToken ct)
    {
        var ifMatch = Request.Headers[HeaderNames.IfMatch].ToString();

        var result = await handler.HandleAsync(
            id, request, string.IsNullOrWhiteSpace(ifMatch) ? null : ifMatch, ct);

        return result.IsSuccess
            ? NoContent()
            : result.Error!.ToProblem(HttpContext);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.CanDeleteProducts)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute][Range(1, int.MaxValue, ErrorMessage = "id must be 1 or greater.")] int id,
        [FromServices] DeleteProductHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, ct);

        return result.IsSuccess
            ? NoContent()
            : result.Error!.ToProblem(HttpContext);
    }

    private static PagedResponse<ProductDto> ToResponse(PagedResult<ProductDto> page) =>
        new(page.Items, page.Page, page.PageSize, page.TotalCount,
            page.TotalPages, page.HasNextPage, page.HasPreviousPage);
}
