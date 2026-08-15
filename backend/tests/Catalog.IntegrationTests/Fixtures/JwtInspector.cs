namespace Catalog.IntegrationTests.Fixtures;

/// <summary>
/// Reads and forges access tokens, so the token-validation tests can produce the exact failure
/// they mean to test — a wrong key, a wrong audience, an expiry in the past — instead of
/// approximating it by corrupting a real token and hoping the right check fires first.
/// </summary>
internal static class JwtInspector
{
    public static IReadOnlyDictionary<string, string> ReadClaims(string token)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var claims = jwt.Claims
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.First().Value);

        claims["iss"] = jwt.Issuer;
        claims["aud"] = jwt.Audiences.FirstOrDefault() ?? string.Empty;

        return claims;
    }

    public static string Forge(
        string signingKey = CatalogApiFactory.TestSigningKey,
        string issuer = CatalogApiFactory.TestIssuer,
        string audience = CatalogApiFactory.TestAudience,
        DateTime? expiresAt = null,
        Guid? subject = null,
        string role = "User")
    {
        var expires = expiresAt ?? DateTime.UtcNow.AddMinutes(15);

        var notBefore = expires.AddMinutes(-30);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, (subject ?? Guid.CreateVersion7()).ToString()),
                new Claim(JwtRegisteredClaimNames.Email, "forged@example.com"),
                new Claim("role", role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: notBefore,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
