namespace Catalog.UnitTests.Application;

/// <summary>
/// The rules Data Annotations structurally cannot express (§3.0.3): password complexity without
/// a fragile regex, and a relationship between two fields.
/// </summary>
public sealed class ValidatorTests
{
    private readonly RegisterRequestValidator _register = new();
    private readonly ProductSearchQueryValidator _search = new();

    private static RegisterRequest Registration(string password) => new()
    {
        Email = "bob@example.com",
        Password = password,
        ConfirmPassword = password
    };

    [Theory]
    [InlineData("Str0ngPassw0rd!")]
    [InlineData("Aa1aaaaa")]
    [InlineData("CorrectHorse9")]
    public void Accepts_a_password_with_upper_lower_and_a_digit(string password)
    {
        _register.Validate(Registration(password)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("alllowercase1", "uppercase")]
    [InlineData("ALLUPPERCASE1", "lowercase")]
    [InlineData("NoDigitsAtAll", "digit")]
    public void Rejects_a_password_missing_a_character_class(string password, string expectedHint)
    {
        var result = _register.Validate(Registration(password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain(expectedHint);
    }

    [Theory]
    [InlineData("password123")]
    [InlineData("Password123")]
    [InlineData("qwerty123")]
    [InlineData("welcome1")]
    public void Rejects_a_password_on_the_common_denylist(string password)
    {
        var result = _register.Validate(Registration(password));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("too common"));
    }

    [Fact]
    public void Skips_complexity_rules_when_the_password_is_absent()
    {
        var result = _register.Validate(new RegisterRequest
        {
            Email = "bob@example.com",
            Password = "",
            ConfirmPassword = ""
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Accepts_a_well_ordered_price_range()
    {
        var query = new ProductSearchQuery { MinPrice = 10m, MaxPrice = 100m };

        _search.Validate(query).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_an_inverted_price_range_against_minPrice()
    {
        var result = _search.Validate(new ProductSearchQuery { MinPrice = 500m, MaxPrice = 100m });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(ProductSearchQuery.MinPrice));
    }

    [Fact]
    public void Accepts_a_range_with_either_bound_absent()
    {
        _search.Validate(new ProductSearchQuery { MinPrice = 10m }).IsValid.Should().BeTrue();
        _search.Validate(new ProductSearchQuery { MaxPrice = 10m }).IsValid.Should().BeTrue();
        _search.Validate(new ProductSearchQuery()).IsValid.Should().BeTrue();
    }
}
