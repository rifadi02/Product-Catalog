namespace Catalog.Application.Common.Interfaces;

/// <summary>Issued access token plus the metadata the SPA needs to schedule its refresh.</summary>
public sealed record AccessToken(string Value, DateTime ExpiresAtUtc, int ExpiresInSeconds);

/// <summary>
/// A freshly minted refresh token. <see cref="Plaintext"/> is returned to the caller exactly
/// once and is never persisted; only <see cref="Hash"/> reaches the database (§4.1).
/// </summary>
public sealed record RefreshTokenPair(string Plaintext, string Hash);

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(User user);
}

public interface IRefreshTokenFactory
{
    /// <summary>256-bit CSPRNG value, Base64Url-encoded, paired with its SHA-256 hash.</summary>
    RefreshTokenPair Create();

    /// <summary>SHA-256 of a presented token, Base64-encoded — the database lookup key.</summary>
    string HashOf(string plaintextToken);
}
