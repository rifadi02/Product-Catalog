namespace Catalog.Infrastructure.Identity;

/// <summary>§4.3 — mints the HS256 access token.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(User user)
    {
        var now = clock.UtcNow;
        var expires = now.Add(_options.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email.Value),
            new("role", user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                EpochSeconds(now).ToString(), ClaimValueTypes.Integer64)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessToken(value, expires, (int)_options.AccessTokenLifetime.TotalSeconds);
    }

    private static long EpochSeconds(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
