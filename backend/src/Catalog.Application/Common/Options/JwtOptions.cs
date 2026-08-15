namespace Catalog.Application.Common.Options;

/// <summary>
/// §4.2. Bound from the <c>Jwt</c> configuration section and validated at startup — a signing
/// key that is too short must fail the first time the process starts, not the first time
/// somebody logs in.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    /// <summary>
    /// HS256 signing key. At least 32 bytes (256 bits) — <c>SymmetricSecurityKey</c> rejects
    /// anything shorter than the hash output, and it is right to.
    /// </summary>
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters for HS256.")]
    public required string SigningKey { get; init; }

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 7;

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenMinutes);
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(RefreshTokenDays);
}
