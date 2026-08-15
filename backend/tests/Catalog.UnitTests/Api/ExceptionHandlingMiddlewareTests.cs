namespace Catalog.UnitTests.Api;

/// <summary>
/// The last line of defence: whatever escapes a handler becomes an HTTP response here.
///
/// <para>Worth testing in isolation because most of these branches are, by design, unreachable
/// from an integration test — <c>RegisterHandler</c> checks for a duplicate email before the
/// insert, so the unique-violation mapping only ever runs when two requests race, and no test
/// suite can reliably produce that on demand. Untested, the fallback that turns that race into a
/// clean 409 would be discovered in production as a 500.</para>
/// </summary>
public sealed class ExceptionHandlingMiddlewareTests
{
    private readonly CapturingLogger<ExceptionHandlingMiddleware> _logger = new();

    private static HttpContext Request()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-for-test" };

        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/products";
        context.Response.Body = new MemoryStream();

        return context;
    }

    private async Task<(int Status, string Code, string Detail, string ContentType)> Handle(
        Exception thrown, string? environmentName = null)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName ?? Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw thrown, environment, _logger);

        var context = Request();
        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);

        return (context.Response.StatusCode,
                document.RootElement.GetProperty("code").GetString()!,
                document.RootElement.GetProperty("detail").GetString()!,
                context.Response.ContentType!);
    }

    [Fact]
    public async Task A_request_that_does_not_throw_is_left_alone()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);

        var middleware = new ExceptionHandlingMiddleware(
            context => { context.Response.StatusCode = StatusCodes.Status200OK; return Task.CompletedTask; },
            environment, _logger);

        var context = Request();
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _logger.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task A_domain_validation_failure_is_a_400_carrying_its_own_message()
    {
        var result = await Handle(new DomainValidationException("Price must not be negative."));

        result.Status.Should().Be(StatusCodes.Status400BadRequest);
        result.Code.Should().Be("validation_failed");
        result.Detail.Should().Be("Price must not be negative.");
        result.ContentType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task A_JSON_failure_that_escapes_the_formatter_is_still_a_400()
    {
        var result = await Handle(new JsonException("unexpected token"));

        result.Status.Should().Be(StatusCodes.Status400BadRequest);
        result.Code.Should().Be("malformed_json");
        result.Detail.Should().NotContain("unexpected token",
            "a parser's internal message is not something to hand a client");
    }

    [Fact]
    public async Task A_NotFoundException_is_a_404()
    {
        var result = await Handle(new NotFoundException("Product", 9));

        result.Status.Should().Be(StatusCodes.Status404NotFound);
        result.Code.Should().Be("resource_not_found");
        result.Detail.Should().Be("Product with id '9' was not found.");
    }

    [Fact]
    public async Task A_lost_registration_race_becomes_a_409_rather_than_a_500()
    {
        var result = await Handle(UniqueViolation("ux_users_email"));

        result.Status.Should().Be(StatusCodes.Status409Conflict);
        result.Code.Should().Be("email_already_registered");
    }

    [Fact]
    public async Task A_unique_violation_on_a_different_constraint_is_not_reported_as_a_duplicate_email()
    {
        var result = await Handle(UniqueViolation("ux_products_sku"));

        result.Status.Should().Be(StatusCodes.Status500InternalServerError);
        result.Code.Should().Be("internal_error",
            "guessing at the cause of an unrecognised constraint would mislead the client");
    }

    [Fact]
    public async Task A_concurrency_conflict_is_a_409()
    {
        var result = await Handle(new DbUpdateConcurrencyException("row changed"));

        result.Status.Should().Be(StatusCodes.Status409Conflict);
        result.Code.Should().Be("concurrency_conflict");
    }

    [Fact]
    public async Task A_cancelled_request_is_499_and_not_an_error()
    {
        var result = await Handle(new OperationCanceledException());

        result.Status.Should().Be(499);
        result.Code.Should().Be("request_cancelled");

        _logger.Messages.Should().ContainSingle()
               .Which.Should().Contain("request_cancelled",
                   "a client hanging up is a warning, not a 500-level alert");
    }

    [Fact]
    public async Task An_unexpected_failure_hides_its_detail_outside_development()
    {
        var result = await Handle(new InvalidOperationException("connection string: Password=hunter2"));

        result.Status.Should().Be(StatusCodes.Status500InternalServerError);
        result.Code.Should().Be("internal_error");
        result.Detail.Should().Be("An unexpected error occurred.");
        result.Detail.Should().NotContain("hunter2", "a stack trace can carry secrets");
    }

    [Fact]
    public async Task An_unexpected_failure_shows_its_stack_trace_in_development()
    {
        var result = await Handle(
            new InvalidOperationException("the thing broke"), Environments.Development);

        result.Status.Should().Be(StatusCodes.Status500InternalServerError);
        result.Detail.Should().Contain("the thing broke")
              .And.Contain(nameof(InvalidOperationException));
    }

    private static DbUpdateException UniqueViolation(string constraintName) =>
        new("insert failed", new PostgresException(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: PostgresErrorCodes.UniqueViolation,
            constraintName: constraintName));
}
