namespace Catalog.UnitTests.Domain;

public sealed class PriceRangeTests
{
    [Fact]
    public void Both_bounds_absent_is_unbounded()
    {
        var result = PriceRange.Create(null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsUnbounded.Should().BeTrue();
    }

    [Theory]
    [InlineData(10d, null)]
    [InlineData(null, 10d)]
    [InlineData(10d, 20d)]
    [InlineData(10d, 10d)]
    [InlineData(0d, 0d)]
    public void Accepts_valid_combinations(double? min, double? max)
    {
        var result = PriceRange.Create((decimal?)min, (decimal?)max);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_negative_minimum()
    {
        var result = PriceRange.Create(-1m, null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Fields.Should().ContainKey("minPrice");
    }

    [Fact]
    public void Rejects_a_negative_maximum()
    {
        var result = PriceRange.Create(null, -1m);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Fields.Should().ContainKey("maxPrice");
    }

    [Fact]
    public void Rejects_an_inverted_range()
    {
        var result = PriceRange.Create(500m, 100m);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Be("minPrice must be less than or equal to maxPrice.");

        result.Error.Fields.Should().ContainKey("minPrice");
    }
}
