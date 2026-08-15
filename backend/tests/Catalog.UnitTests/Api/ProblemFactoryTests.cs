namespace Catalog.UnitTests.Api;

/// <summary>
/// The correlation identifier stitched into every error response.
///
/// <para>Its fallback chain is unreachable from an integration test, because Kestrel always
/// assigns a <c>TraceIdentifier</c> — so the first branch always wins and the other two never run.
/// They exist for the cases where nothing assigned one, and a support request that arrives quoting
/// a blank traceId is a support request nobody can trace.</para>
/// </summary>
public sealed class ProblemFactoryTests
{
    [Fact]
    public void The_request_trace_identifier_is_preferred()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "0HN7A:00000001" };

        ProblemFactory.TraceId(context).Should().Be("0HN7A:00000001");
    }

    [Fact]
    public void The_ambient_activity_is_the_fallback_when_nothing_assigned_one()
    {
        var context = new DefaultHttpContext { TraceIdentifier = string.Empty };

        using var activity = new Activity("test-request").Start();

        ProblemFactory.TraceId(context).Should().Be(activity.Id)
            .And.NotBeNullOrWhiteSpace(
                "distributed tracing already has an id; inventing a second one loses the join");
    }

    [Fact]
    public void With_neither_a_trace_identifier_nor_an_activity_the_id_is_empty_not_null()
    {
        Activity.Current?.Stop();
        Activity.Current = null;

        var context = new DefaultHttpContext { TraceIdentifier = string.Empty };

        ProblemFactory.TraceId(context).Should().BeEmpty(
            "the traceId field is always present in the contract, even when it has nothing to say");
    }

    [Fact]
    public void A_problem_document_carries_the_code_and_the_trace_id()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };

        var problem = ProblemFactory.Create(
            context, StatusCodes.Status404NotFound, "resource_not_found", "Product 9 was not found.");

        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Extensions["code"].Should().Be("resource_not_found");
        problem.Extensions["traceId"].Should().Be("trace-1");
        problem.Extensions.Should().NotContainKey("errors", "there are no field errors to report");
    }

    [Fact]
    public void Field_errors_are_attached_only_when_there_are_some()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };

        var problem = ProblemFactory.Create(
            context, StatusCodes.Status400BadRequest, "validation_failed",
            "One or more validation errors occurred.",
            new Dictionary<string, string[]> { ["price"] = ["Price must be 0 or greater."] });

        problem.Extensions.Should().ContainKey("errors");
    }

    [Theory]
    [InlineData(ErrorKind.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorKind.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.None, StatusCodes.Status500InternalServerError)]
    public void Every_error_kind_maps_to_its_status_code(ErrorKind kind, int expected)
    {
        kind.ToStatusCode().Should().Be(expected);
    }
}
