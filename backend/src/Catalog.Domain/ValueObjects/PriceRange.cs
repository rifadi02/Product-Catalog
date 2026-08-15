namespace Catalog.Domain.ValueObjects;

/// <summary>
/// An optional-bounded, validated price filter. Encapsulates the min &lt;= max rule that
/// Data Annotations structurally cannot express (it is a relationship between two fields).
/// </summary>
public readonly record struct PriceRange
{
    public decimal? Min { get; }
    public decimal? Max { get; }

    private PriceRange(decimal? min, decimal? max) => (Min, Max) = (min, max);

    public static readonly PriceRange Unbounded = new(null, null);

    public static Result<PriceRange> Create(decimal? min, decimal? max)
    {
        if (min is < 0) return Result<PriceRange>.Invalid("minPrice", "minPrice must be greater than or equal to 0.");
        if (max is < 0) return Result<PriceRange>.Invalid("maxPrice", "maxPrice must be greater than or equal to 0.");
        if (min is not null && max is not null && min > max)
            return Result<PriceRange>.Invalid("minPrice", "minPrice must be less than or equal to maxPrice.");

        return Result<PriceRange>.Success(new PriceRange(min, max));
    }

    public bool IsUnbounded => Min is null && Max is null;
}
