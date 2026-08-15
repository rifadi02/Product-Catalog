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

    /// <summary>The single serialisation of a problem document, so a body written by middleware
    /// and one returned by a controller are byte-for-byte the same shape.</summary>
    public static string Serialize(ProblemDetails problem) => JsonSerializer.Serialize(problem, Json);

    /// <param name="response">The response to reset and write the problem document to.</param>
    /// <param name="status">HTTP status code.</param>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="detail">Human-readable explanation.</param>
    /// <param name="errors">Optional field-scoped validation messages.</param>
    /// <param name="configureHeaders">
    /// Applied after the response is reset and before it is committed. It exists because
    /// <c>HttpResponse.Clear()</c> discards headers set earlier in the request — which is
    /// correct (a half-written 200's headers must not survive onto a 401) but silently ate
    /// <c>x-token-expired</c>, the one header the SPA needs in order to tell "refresh me" apart
    /// from "log in again". Any header that belongs to the problem response has to be set here,
    /// on the far side of the reset.
    /// </param>
    public static async Task WriteProblemAsync(
        this HttpResponse response,
        int status,
        string code,
        string detail,
        IDictionary<string, string[]>? errors = null,
        Action<IHeaderDictionary>? configureHeaders = null)
    {
        if (response.HasStarted) return;

        var problem = Create(response.HttpContext, status, code, detail, errors);

        response.Clear();
        response.StatusCode = status;
        response.ContentType = "application/problem+json";

        configureHeaders?.Invoke(response.Headers);

        await response.WriteAsync(Serialize(problem));
    }
}
