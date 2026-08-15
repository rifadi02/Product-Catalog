namespace Catalog.Domain.Enums;

/// <summary>Authorization role. Persisted as a string, not an int — see §2.2.2.</summary>
public enum UserRole
{
    User = 0,
    Admin = 1
}
