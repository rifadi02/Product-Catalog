namespace Catalog.Domain.Enums;

/// <summary>
/// Whitelist of sortable columns. Exists so the API can accept a sort key from the
/// query string without ever concatenating user input into SQL or an EF expression.
/// </summary>
public enum ProductSortField
{
    CreatedAt = 0,
    Name = 1,
    Price = 2
}

public enum SortDirection
{
    Asc = 0,
    Desc = 1
}
