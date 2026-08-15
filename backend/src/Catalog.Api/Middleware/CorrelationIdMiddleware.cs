namespace Catalog.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = TryReadSuppliedId(context)
                            ?? Activity.Current?.Id
                            ?? context.TraceIdentifier;

        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private const int MaxLength = 128;

    private static string? TryReadSuppliedId(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var values))
            return null;

        var supplied = values.ToString();

        return string.IsNullOrWhiteSpace(supplied)
            ? null
            : supplied[..Math.Min(supplied.Length, MaxLength)];
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
