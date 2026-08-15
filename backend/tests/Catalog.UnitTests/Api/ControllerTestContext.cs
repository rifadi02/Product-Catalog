namespace Catalog.UnitTests.Api;

/// <summary>
/// Shared scaffolding for the controller tests.
///
/// <para>Controllers are given a <see cref="DefaultHttpContext"/> rather than a test host. What is
/// under test here is the part of the API the integration suite can only observe through a socket:
/// which header is read, which is written, and what an <see cref="Error"/> turns into. Those are
/// decisions the controller makes on its own, and they deserve a test that fails on the line that
/// broke rather than three layers away.</para>
///
/// <para>Handlers are real, wired to substituted repositories. They are sealed classes with no
/// interface to stand in for, and faking them would mean asserting that the controller called
/// something — not that it produced the right response.</para>
/// </summary>
internal static class ControllerTestContext
{
    public static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Attaches a fresh request/response pair and returns the context for arranging headers.</summary>
    public static HttpContext WithHttpContext(this ControllerBase controller)
    {
        var http = new DefaultHttpContext { TraceIdentifier = "trace-for-test" };

        controller.ControllerContext = new ControllerContext { HttpContext = http };

        return http;
    }

    public static ProductDto Dto(int id = 1, string name = "Desk Lamp", decimal price = 24.99m) =>
        new(id, name, "Warm light", price, Now, null);

    /// <summary>Unwraps the problem document a controller returned, whatever result type carries it.</summary>
    public static ProblemDetails ProblemFrom(IActionResult result)
    {
        var content = result.Should().BeOfType<ContentResult>().Subject;

        content.ContentType.Should().Be("application/problem+json",
            "RFC 9457 errors must not be mislabelled as plain application/json");

        return JsonSerializer.Deserialize<ProblemDetails>(
            content.Content!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    /// <summary>The machine-readable <c>code</c> extension, which clients branch on.</summary>
    public static string CodeOf(IActionResult result)
    {
        var content = result.Should().BeOfType<ContentResult>().Subject;

        using var document = JsonDocument.Parse(content.Content!);

        return document.RootElement.GetProperty("code").GetString()!;
    }

    public static int StatusOf(IActionResult result) =>
        result.Should().BeOfType<ContentResult>().Subject.StatusCode!.Value;
}
