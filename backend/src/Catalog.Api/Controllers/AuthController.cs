namespace Catalog.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
public sealed class AuthController(ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        [FromServices] RegisterHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);

        if (!result.IsSuccess)
            return result.Error!.ToProblem(HttpContext);

        return Created($"/api/v1/users/{result.Value!.Id}", result.Value);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] LoginHandler handler,
        CancellationToken ct)
    {
        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await handler.HandleAsync(request, sourceIp, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error!.ToProblem(HttpContext);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        [FromServices] RefreshHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : result.Error!.ToProblem(HttpContext);
    }

    [HttpPost("logout")]
    [Authorize(Policy = Policies.Authenticated)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest request,
        [FromServices] LogoutHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);

        return result.IsSuccess
            ? NoContent()
            : result.Error!.ToProblem(HttpContext);
    }

    [HttpGet("me")]
    [Authorize(Policy = Policies.Authenticated)]
    [ProducesResponseType<UserSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        if (currentUser.UserId is not { } userId)
        {
            return Catalog.Domain.Common.Error
                .Unauthorized("invalid_token", "The access token does not carry a usable subject claim.")
                .ToProblem(HttpContext);
        }

        return Ok(new UserSummary(
            userId,
            currentUser.Email ?? string.Empty,
            currentUser.Role?.ToString() ?? string.Empty));
    }
}
