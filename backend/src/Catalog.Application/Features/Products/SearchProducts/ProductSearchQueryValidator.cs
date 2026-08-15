namespace Catalog.Application.Features.Products.SearchProducts;

/// <summary>
/// The cross-field rule from §3.6. Data Annotations validate one property at a time and
/// structurally cannot express a relationship between two of them, so this runs in the
/// validation filter alongside them.
///
/// The rule itself is not restated here — it is delegated to <see cref="PriceRange.Create"/>,
/// which is the single source of truth and is unit-tested without a web host.
/// </summary>
public sealed class ProductSearchQueryValidator : AbstractValidator<ProductSearchQuery>
{
    public ProductSearchQueryValidator()
    {
        RuleFor(x => x.MinPrice)
            .Custom((_, context) =>
            {
                var query = context.InstanceToValidate;
                var range = PriceRange.Create(query.MinPrice, query.MaxPrice);

                if (!range.IsSuccess)
                    context.AddFailure(nameof(ProductSearchQuery.MinPrice), range.Error!.Message);
            });
    }
}
