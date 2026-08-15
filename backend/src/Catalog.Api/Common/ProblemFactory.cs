namespace Catalog.Api.Common;

public static class ProblemFactory
{
    private const string StatusTypeBase = "https://datatracker.ietf.org/doc/html/rfc9110#section-15";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal)
    {
        ["validation_failed"] = "Validation failed",
        ["malformed_json"] = "Malformed request body",
        ["invalid_credentials"] = "Invalid credentials",
        ["missing_token"] = "Authentication required",
        ["invalid_token"] = "Invalid token",
        ["token_expired"] = "Token expired",
        ["refresh_token_invalid"] = "Invalid refresh token",
        ["insufficient_role"] = "Forbidden",
        ["resource_not_found"] = "Resource not found",
        ["email_already_registered"] = "Email already registered",
        ["concurrency_conflict"] = "Concurrency conflict",
        ["precondition_required"] = "Precondition required",
        ["rate_limited"] = "Too many requests",
        ["request_cancelled"] = "Request cancelled",
        ["internal_error"] = "Internal server error"
    };

    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string code,
        string detail,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Type = $"{StatusTypeBase}",
            Title = Titles.GetValueOrDefault(code, "Request failed"),
            Status = status,
            Detail = detail,
            Instance = context.Request.Path
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = TraceId(context);

        if (errors is { Count: > 0 })
            problem.Extensions["errors"] = errors;

        return problem;
    }

    public static string TraceId(HttpContext context) =>
        string.IsNullOrEmpty(context.TraceIdentifier)
            ? Activity.Current?.Id ?? string.Empty
            : context.TraceIdentifier;

    public static async Task WriteProblemAsync(
        this HttpResponse response,
        int status,
        string code,
        string detail,
        IDictionary<string, string[]>? errors = null)
    {
        if (response.HasStarted) return;

        var problem = Create(response.HttpContext, status, code, detail, errors);

        response.Clear();
        response.StatusCode = status;
        response.ContentType = "application/problem+json";

        await response.WriteAsync(JsonSerializer.Serialize(problem, Json));
    }
}
