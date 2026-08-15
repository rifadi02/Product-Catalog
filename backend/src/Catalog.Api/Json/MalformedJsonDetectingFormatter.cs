namespace Catalog.Api.Json;

/// <summary>
/// Wraps the JSON input formatter and records, on the request, whether the body actually parsed.
///
/// <para>The API distinguishes <c>malformed_json</c> ("this is not JSON") from
/// <c>validation_failed</c> ("this is JSON, but these fields are wrong"), and the two demand
/// different things of a client: fix the serialiser, versus fix a field. Recovering that
/// distinction after the fact is not possible. <c>SystemTextJsonInputFormatter</c> catches the
/// <see cref="System.Text.Json.JsonException"/> and re-throws it as an
/// <c>InputFormatterException</c>, which <c>ModelStateDictionary</c> then treats as a
/// client-safe message and stores as a <em>string</em> — <c>ModelError.Exception</c> is left null.
/// By the time <c>InvalidModelStateResponseFactory</c> runs, every trace of the parse failure is
/// gone, and a request whose body was pure noise was reported as an ordinary field validation
/// error.</para>
///
/// <para>So the fact is captured at the only point that still knows it.</para>
/// </summary>
internal sealed class MalformedJsonDetectingFormatter(IInputFormatter inner)
    : IInputFormatter, IInputFormatterExceptionPolicy
{
    private const string ItemKey = "catalog.malformed-json";

    public bool CanRead(InputFormatterContext context) => inner.CanRead(context);

    public async Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
    {
        var result = await inner.ReadAsync(context);

        if (result.HasError)
            context.HttpContext.Items[ItemKey] = true;

        return result;
    }

    public InputFormatterExceptionPolicy ExceptionPolicy =>
        inner is IInputFormatterExceptionPolicy policy
            ? policy.ExceptionPolicy
            : InputFormatterExceptionPolicy.AllExceptions;

    /// <summary>Whether the body of this request failed to parse as JSON.</summary>
    public static bool BodyWasMalformed(HttpContext context) =>
        context.Items.ContainsKey(ItemKey);
}

public static class MalformedJsonDetectionExtensions
{
    /// <summary>
    /// Replaces the built-in JSON input formatters with wrapped ones. Must run as a
    /// <c>PostConfigure</c>: the formatter list does not exist until MVC's own options setup has
    /// run.
    /// </summary>
    public static IServiceCollection AddMalformedJsonDetection(this IServiceCollection services)
    {
        services.PostConfigure<MvcOptions>(options =>
        {
            for (var i = 0; i < options.InputFormatters.Count; i++)
            {
                if (options.InputFormatters[i] is SystemTextJsonInputFormatter formatter)
                    options.InputFormatters[i] = new MalformedJsonDetectingFormatter(formatter);
            }
        });

        return services;
    }
}
