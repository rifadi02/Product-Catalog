namespace Catalog.UnitTests.Api;

/// <summary>
/// The policy that keeps credentials out of the log files.
///
/// <para>This is the kind of code that fails silently: nothing breaks when redaction stops
/// working, the passwords simply start appearing in the log sink and nobody notices until an
/// audit. It has no integration-test surface either — asserting on it through HTTP would mean
/// asserting on log output — so it is tested here or nowhere.</para>
/// </summary>
public sealed class RedactingDestructuringPolicyTests
{
    private const string Mask = "***REDACTED***";

    private readonly RedactingDestructuringPolicy _policy = new();
    private readonly ILogEventPropertyValueFactory _factory = new ScalarFactory();

    private sealed class ScalarFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects) =>
            new ScalarValue(value);
    }

    private sealed record Credentials(string Email, string Password, string RefreshToken);

    private sealed record Harmless(string Name, int Quantity);

    private sealed class Awkward
    {
        public string Token => "should never be read";
        public string Explodes => throw new InvalidOperationException("boom");
    }

    private IReadOnlyDictionary<string, string?> Destructure(object value)
    {
        _policy.TryDestructure(value, _factory, out var result).Should().BeTrue();

        return result.Should().BeOfType<StructureValue>().Subject
                     .Properties.ToDictionary(
                         p => p.Name,
                         p => (p.Value as ScalarValue)?.Value?.ToString());
    }

    [Fact]
    public void A_secret_is_masked_and_its_neighbours_are_kept()
    {
        var properties = Destructure(
            new Credentials("someone@example.com", "Str0ngPassw0rd!", "refresh-token-value"));

        properties["Password"].Should().Be(Mask);
        properties["RefreshToken"].Should().Be(Mask);

        properties["Email"].Should().Be("someone@example.com",
            "redaction must not blind the log to the context that makes it useful");
    }

    [Fact]
    public void The_secret_never_appears_anywhere_in_the_rendered_structure()
    {
        _policy.TryDestructure(
            new Credentials("someone@example.com", "Str0ngPassw0rd!", "refresh-token-value"),
            _factory, out var result).Should().BeTrue();

        result!.ToString().Should().NotContain("Str0ngPassw0rd!")
               .And.NotContain("refresh-token-value");
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var properties = Destructure(new { PASSWORD = "hunter2", accesstoken = "abc" });

        properties["PASSWORD"].Should().Be(Mask);
        properties["accesstoken"].Should().Be(Mask);
    }

    [Fact]
    public void An_object_with_nothing_sensitive_is_left_to_the_default_formatter()
    {
        _policy.TryDestructure(new Harmless("Desk Lamp", 3), _factory, out var result)
               .Should().BeFalse("rewriting every log event would cost more than it protects");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("a plain string")]
    [InlineData(42)]
    [InlineData(true)]
    public void Primitives_and_framework_types_are_skipped(object value)
    {
        _policy.TryDestructure(value, _factory, out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void A_property_that_throws_is_reported_without_taking_the_log_call_down_with_it()
    {
        var properties = Destructure(new Awkward());

        properties["Token"].Should().Be(Mask, "the masked property is never even read");
        properties["Explodes"].Should().Be("<TargetInvocationException>");
    }
}
