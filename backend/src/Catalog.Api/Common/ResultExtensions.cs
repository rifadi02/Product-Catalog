namespace Catalog.Api.Common;

public static class ResultExtensions
{
    public static int ToStatusCode(this ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };

    public static IActionResult ToProblem(this Error error, HttpContext context)
    {
        var status = error.Kind.ToStatusCode();

        var problem = ProblemFactory.Create(
            context,
            status,
            error.Code,
            error.Message,
            error.Fields?.ToDictionary(kv => kv.Key, kv => kv.Value));

        return problem.ToResult(status);
    }

    /// <summary>
    /// Renders a problem document as a <see cref="ContentResult"/> rather than an
    /// <see cref="ObjectResult"/>.
    ///
    /// <para>Not a stylistic choice. <c>[Produces("application/json")]</c> on the controllers is a
    /// result filter, and it does not merge with a result's own content types — it clears them and
    /// substitutes its own. Every error returned from an action therefore went out as
    /// <c>application/json</c> no matter what <c>ContentTypes</c> said, so the API advertised
    /// RFC 9457 problem documents and shipped them mislabelled, while the 401/403/429 written
    /// directly by middleware were labelled correctly — the same API disagreeing with itself about
    /// its own error format. <see cref="ProducesAttribute"/> only rewrites an
    /// <see cref="ObjectResult"/>, so a <see cref="ContentResult"/> keeps the media type it was
    /// given, and the body is produced by <see cref="ProblemFactory.Serialize"/> — the very
    /// serialiser the middleware path uses.</para>
    /// </summary>
    public static IActionResult ToResult(this ProblemDetails problem, int status) =>
        new ContentResult
        {
            StatusCode = status,
            ContentType = "application/problem+json",
            Content = ProblemFactory.Serialize(problem)
        };
}
