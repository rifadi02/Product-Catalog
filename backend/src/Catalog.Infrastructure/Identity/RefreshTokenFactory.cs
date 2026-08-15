namespace Catalog.Infrastructure.Identity;

/// <summary>
/// §4.1. The refresh token is deliberately <b>not</b> a JWT: it carries no claims, so it needs
/// no signature, and an opaque value can be revoked — which is the whole point of having one.
///
/// Only the SHA-256 hash is persisted. A database dump must not be sufficient to impersonate a
/// session. SHA-256 without a work factor is correct here and wrong for passwords: the input is
/// 256 bits of CSPRNG output, so there is no dictionary to attack and nothing to slow down.
/// </summary>
public sealed class RefreshTokenFactory : IRefreshTokenFactory
{
    private const int TokenBytes = 32;

    public RefreshTokenPair Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        var plaintext = Base64UrlEncode(bytes);

        return new RefreshTokenPair(plaintext, HashOf(plaintext));
    }

    public string HashOf(string plaintextToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plaintextToken));
        return Convert.ToBase64String(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
