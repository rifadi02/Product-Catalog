namespace Catalog.Domain.Entities;

public sealed class Product
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private Product() { }

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Identity of the creating user. Intentionally a bare Guid with no navigation property
    /// and no foreign key — see §1.1. Nullable because seeded/system-created rows have no user.
    /// </summary>
    public Guid? CreatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Optimistic concurrency token. Mapped to PostgreSQL's system column <c>xmin</c>, so it costs
    /// zero storage and is maintained by the server. See §2.2.1 for why this is a uint and not
    /// a byte[].
    /// </summary>
    public uint Version { get; private set; }

    public static Product Create(
        string name,
        string? description,
        decimal price,
        DateTime utcNow,
        Guid? createdBy)
    {
        var normalisedName = (name ?? string.Empty).Trim();

        if (normalisedName.Length is 0)
            throw new DomainValidationException("Product name is required.");
        if (normalisedName.Length > NameMaxLength)
            throw new DomainValidationException($"Product name must not exceed {NameMaxLength} characters.");
        if (description is { Length: > DescriptionMaxLength })
            throw new DomainValidationException($"Product description must not exceed {DescriptionMaxLength} characters.");
        if (price < 0m)
            throw new DomainValidationException("Product price must be greater than or equal to 0.");

        return new Product
        {
            Name = normalisedName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Price = decimal.Round(price, 2, MidpointRounding.ToEven),
            CreatedAt = utcNow,
            CreatedBy = createdBy,
            IsDeleted = false
        };
    }

    public void Update(string name, string? description, decimal price, DateTime utcNow)
    {
        if (IsDeleted)
            throw new DomainValidationException("A deleted product cannot be modified.");

        var candidate = Create(name, description, price, utcNow, CreatedBy);

        Name = candidate.Name;
        Description = candidate.Description;
        Price = candidate.Price;
        UpdatedAt = utcNow;
    }

    public void SoftDelete(DateTime utcNow)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = utcNow;
        UpdatedAt = utcNow;
    }
}
