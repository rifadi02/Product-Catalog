namespace Catalog.Domain.Enums;

/// <summary>Derived, never persisted. Computed from ExpiresAt / RevokedAt.</summary>
public enum RefreshTokenStatus
{
    Active = 0,
    Expired = 1,
    Revoked = 2
}
