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

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}
