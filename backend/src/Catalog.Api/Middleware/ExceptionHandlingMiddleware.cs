namespace Catalog.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    IHostEnvironment environment,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, code, detail) = Map(ex);

        if (status >= StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogWarning("{Code} for {Method} {Path}: {Detail}", code, context.Request.Method, context.Request.Path, detail);

        await context.Response.WriteProblemAsync(status, code, detail);
    }

    private (int Status, string Code, string Detail) Map(Exception ex) => ex switch
    {
        DomainValidationException e =>
            (StatusCodes.Status400BadRequest, "validation_failed", e.Message),

        JsonException =>
            (StatusCodes.Status400BadRequest, "malformed_json", "The request body is not valid JSON."),

        NotFoundException e =>
            (StatusCodes.Status404NotFound, "resource_not_found", e.Message),

        DbUpdateException e when IsUniqueViolation(e, "ux_users_email") =>
            (StatusCodes.Status409Conflict, "email_already_registered",
             "An account with this email already exists."),

        DbUpdateConcurrencyException =>
            (StatusCodes.Status409Conflict, "concurrency_conflict",
             "The resource was modified by another request."),

        OperationCanceledException =>
            (499, "request_cancelled", "The request was cancelled."),

        _ => (StatusCodes.Status500InternalServerError, "internal_error",
              environment.IsDevelopment() ? ex.ToString() : "An unexpected error occurred.")
    };

    private static bool IsUniqueViolation(DbUpdateException ex, string constraintName) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg &&
        (pg.ConstraintName?.Contains(constraintName, StringComparison.OrdinalIgnoreCase) ?? false);
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
