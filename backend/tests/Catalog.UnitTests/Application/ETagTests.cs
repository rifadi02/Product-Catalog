namespace Catalog.UnitTests.Application;

public sealed class ETagTests
{
    [Fact]
    public void Formats_a_version_as_a_strong_quoted_tag()
    {
        ETag.From(8125).Should().Be("\"8125\"");
    }

    [Theory]
    [InlineData("\"8125\"")]
    [InlineData("8125")]
    [InlineData("W/\"8125\"")]
    [InlineData(" \"8125\" ")]
    [InlineData("\"1\", \"8125\"")]
    public void Matches_the_current_version_in_every_form_a_client_might_send(string header)
    {
        ETag.Matches(header, 8125).Should().BeTrue();
    }

    [Fact]
    public void Star_matches_any_existing_resource()
    {
        ETag.Matches("*", 8125).Should().BeTrue();
    }

    [Theory]
    [InlineData("\"8124\"")]
    [InlineData("\"\"")]
    [InlineData("garbage")]
    public void Does_not_match_a_different_version(string header)
    {
        ETag.Matches(header, 8125).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_absent_header_does_not_match(string? header)
    {
        ETag.Matches(header, 8125).Should().BeFalse();
    }
}
