namespace Catalog.UnitTests.Application;

public sealed class LikePatternTests
{
    [Fact]
    public void Escapes_the_percent_wildcard()
    {
        LikePattern.Contains("100%").Should().Be(@"%100\%%");
    }

    [Fact]
    public void Escapes_the_underscore_wildcard()
    {
        LikePattern.Contains("a_b").Should().Be(@"%a\_b%");
    }

    [Fact]
    public void Escapes_the_escape_character_itself()
    {
        LikePattern.Contains(@"a\b").Should().Be(@"%a\\b%");
    }

    [Fact]
    public void Escapes_the_backslash_before_the_wildcards_it_introduces()
    {
        LikePattern.Escape(@"50%\_off").Should().Be(@"50\%\\\_off");
    }

    [Fact]
    public void Leaves_ordinary_text_alone()
    {
        LikePattern.Contains("desk lamp").Should().Be("%desk lamp%");
    }
}
