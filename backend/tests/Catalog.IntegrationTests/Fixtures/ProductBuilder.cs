namespace Catalog.IntegrationTests.Fixtures;

/// <summary>
/// §2.4.6. Integration tests build their own data rather than reusing <c>DatabaseSeeder</c>: a
/// test that depends on demo data is a test that breaks when the demo data changes.
///
/// Bogus is still useful here for property-style volume checks ("a search over 1,000 random
/// products never returns a soft-deleted row") — but never for asserting on a specific value.
/// </summary>
internal sealed class ProductBuilder
{
    private string _name = "Test Product";
    private string? _description;
    private decimal _price = 10.00m;
    private DateTime _createdAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private Guid? _createdBy;
    private bool _deleted;

    public ProductBuilder Named(string name) { _name = name; return this; }
    public ProductBuilder Priced(decimal price) { _price = price; return this; }
    public ProductBuilder Describing(string? description) { _description = description; return this; }
    public ProductBuilder CreatedAt(DateTime createdAt) { _createdAt = createdAt; return this; }
    public ProductBuilder CreatedBy(Guid? userId) { _createdBy = userId; return this; }
    public ProductBuilder SoftDeleted() { _deleted = true; return this; }

    public Product Build()
    {
        var product = Product.Create(_name, _description, _price, _createdAt, _createdBy);

        if (_deleted)
            product.SoftDelete(_createdAt.AddDays(1));

        return product;
    }
}
