namespace Catalog.UnitTests.Domain;

public sealed class ProductTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Author = Guid.CreateVersion7();

    [Fact]
    public void Create_trims_the_name()
    {
        var product = Product.Create("  Desk Lamp  ", null, 10m, Now, Author);

        product.Name.Should().Be("Desk Lamp");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_rejects_a_blank_name(string name)
    {
        var act = () => Product.Create(name, null, 10m, Now, Author);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("Product name is required.");
    }

    [Fact]
    public void Create_rejects_a_name_over_the_limit()
    {
        var act = () => Product.Create(new string('x', Product.NameMaxLength + 1), null, 10m, Now, Author);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Create_accepts_a_name_exactly_at_the_limit()
    {
        var product = Product.Create(new string('x', Product.NameMaxLength), null, 10m, Now, Author);

        product.Name.Should().HaveLength(Product.NameMaxLength);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_stores_a_blank_description_as_null(string? description)
    {
        var product = Product.Create("Lamp", description, 10m, Now, Author);

        product.Description.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_a_negative_price()
    {
        var act = () => Product.Create("Lamp", null, -0.01m, Now, Author);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("Product price must be greater than or equal to 0.");
    }

    [Fact]
    public void Create_accepts_a_zero_price()
    {
        var product = Product.Create("Free Sample", null, 0m, Now, Author);

        product.Price.Should().Be(0m);
    }

    [Theory]
    [InlineData(10.005, 10.00)]
    [InlineData(10.015, 10.02)]
    [InlineData(10.014, 10.01)]
    [InlineData(10.016, 10.02)]
    public void Create_rounds_the_price_to_two_decimal_places(double input, double expected)
    {
        var product = Product.Create("Lamp", null, (decimal)input, Now, Author);

        product.Price.Should().Be((decimal)expected);
    }

    [Fact]
    public void Create_leaves_UpdatedAt_null()
    {
        var product = Product.Create("Lamp", null, 10m, Now, Author);

        product.CreatedAt.Should().Be(Now);
        product.UpdatedAt.Should().BeNull();
        product.IsDeleted.Should().BeFalse();
        product.CreatedBy.Should().Be(Author);
    }

    [Fact]
    public void Update_stamps_UpdatedAt_and_leaves_creation_metadata_alone()
    {
        var product = Product.Create("Lamp", "old", 10m, Now, Author);
        var later = Now.AddHours(3);

        product.Update("Desk Lamp", "new", 25.50m, later);

        product.Name.Should().Be("Desk Lamp");
        product.Description.Should().Be("new");
        product.Price.Should().Be(25.50m);
        product.UpdatedAt.Should().Be(later);

        product.CreatedAt.Should().Be(Now, "CreatedAt is immutable");
        product.CreatedBy.Should().Be(Author, "CreatedBy is immutable");
    }

    [Fact]
    public void Update_refuses_to_modify_a_deleted_product()
    {
        var product = Product.Create("Lamp", null, 10m, Now, Author);
        product.SoftDelete(Now);

        var act = () => product.Update("Lamp", null, 12m, Now.AddHours(1));

        act.Should().Throw<DomainValidationException>()
            .WithMessage("A deleted product cannot be modified.");
    }

    [Fact]
    public void Update_enforces_the_same_invariants_as_Create()
    {
        var product = Product.Create("Lamp", null, 10m, Now, Author);

        var act = () => product.Update("  ", null, 10m, Now);

        act.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void SoftDelete_marks_the_product_and_stamps_both_timestamps()
    {
        var product = Product.Create("Lamp", null, 10m, Now, Author);
        var later = Now.AddDays(1);

        product.SoftDelete(later);

        product.IsDeleted.Should().BeTrue();
        product.DeletedAt.Should().Be(later);
        product.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void SoftDelete_is_idempotent()
    {
        var product = Product.Create("Lamp", null, 10m, Now, Author);
        var first = Now.AddDays(1);

        product.SoftDelete(first);
        product.SoftDelete(Now.AddDays(2));

        product.DeletedAt.Should().Be(first);
    }
}
