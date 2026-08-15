namespace Catalog.UnitTests.Domain;

public sealed class EmailTests
{
    [Theory]
    [InlineData("bob@example.com", "bob@example.com")]
    [InlineData("Bob@Example.COM", "bob@example.com")]
    [InlineData("  bob@example.com  ", "bob@example.com")]
    [InlineData("\tBOB@EXAMPLE.CO.UK\n", "bob@example.co.uk")]
    public void Create_normalises_to_trimmed_lowercase(string input, string expected)
    {
        var result = Email.Create(input);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(expected);
    }

    [Fact]
    public void Normalisation_makes_case_variants_equal()
    {
        var a = Email.Create("Bob@X.com").Value;
        var b = Email.Create("bob@x.COM").Value;

        a.Should().Be(b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_missing_address(string? input)
    {
        var result = Email.Create(input);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Be("Email is required.");
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("@example.com")]
    [InlineData("bob@")]
    [InlineData("bob@example")]
    [InlineData("bob @example.com")]
    [InlineData("bob@@example.com")]
    public void Create_rejects_input_that_is_obviously_not_an_address(string input)
    {
        var result = Email.Create(input);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Be("Email is not a valid address.");
    }

    [Fact]
    public void Create_rejects_an_address_over_the_length_limit()
    {
        var tooLong = new string('a', Email.MaxLength) + "@example.com";

        var result = Email.Create(tooLong);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("256");
    }

    [Fact]
    public void FromTrusted_skips_validation()
    {
        var email = Email.FromTrusted("legacy-value-that-would-not-validate");

        email.Value.Should().Be("legacy-value-that-would-not-validate");
    }

    [Fact]
    public void Implicitly_converts_to_its_string_value()
    {
        string value = Email.Create("bob@example.com").Value;

        value.Should().Be("bob@example.com");
    }
}
