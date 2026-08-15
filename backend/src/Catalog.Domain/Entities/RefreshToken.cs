namespace Catalog.Domain.Entities;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>
    /// SHA-256 of the opaque token, Base64-encoded (44 chars). The plaintext token is returned
    /// to the caller exactly once and is never persisted — a database dump must not be
    /// sufficient to impersonate a session.
    /// </summary>
    public string TokenHash { get; private set; } = null!;

    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? RevocationReason { get; private set; }

    public User User { get; private set; } = null!;

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTime utcNow, TimeSpan lifetime)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TokenHash = tokenHash,
            CreatedAt = utcNow,
            ExpiresAt = utcNow.Add(lifetime)
        };

    public RefreshTokenStatus StatusAt(DateTime utcNow)
        => RevokedAt is not null ? RefreshTokenStatus.Revoked
         : ExpiresAt <= utcNow ? RefreshTokenStatus.Expired
         : RefreshTokenStatus.Active;

    public bool IsActiveAt(DateTime utcNow) => StatusAt(utcNow) is RefreshTokenStatus.Active;

    public void Revoke(DateTime utcNow, string reason, string? replacedByTokenHash = null)
    {
        if (RevokedAt is not null) return;
        RevokedAt = utcNow;
        RevocationReason = reason;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}

/// <summary>Stable revocation reasons. Strings in a table dump; constants in code.</summary>
public static class RevocationReasons
{
    public const string Rotated = "rotated";
    public const string Logout = "logout";
    public const string ReuseDetected = "reuse_detected";
}
