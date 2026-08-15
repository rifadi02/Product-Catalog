namespace Catalog.Application.Features.Auth;

/// <summary>Body of <c>POST /api/v1/auth/register</c> (§3.1).</summary>
public sealed record RegisterRequest
{
    /// <summary>Normalised to lower-case and trimmed before the uniqueness check.</summary>
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid address.")]
    [MaxLength(256)]
    public required string Email { get; init; }

    /// <summary>Minimum 8 characters. Complexity is enforced by a FluentValidation rule.</summary>
    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128, MinimumLength = 8,
        ErrorMessage = "Password must be between 8 and 128 characters.")]
    [DataType(DataType.Password)]
    public required string Password { get; init; }

    /// <summary>Must equal <see cref="Password"/>.</summary>
    [Required(ErrorMessage = "ConfirmPassword is required.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    public required string ConfirmPassword { get; init; }

}

public sealed record RegisterResponse(Guid Id, string Email, string Role, DateTime CreatedAt);

/// <summary>Body of <c>POST /api/v1/auth/login</c> (§3.2).</summary>
public sealed record LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid address.")]
    [MaxLength(256)]
    public required string Email { get; init; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128, MinimumLength = 1)]
    [DataType(DataType.Password)]
    public required string Password { get; init; }
}

/// <summary>Body of <c>POST /api/v1/auth/refresh</c> and <c>POST /api/v1/auth/logout</c>.</summary>
public sealed record RefreshTokenRequest
{
    [Required(ErrorMessage = "RefreshToken is required.")]
    [StringLength(512, MinimumLength = 16)]
    public required string RefreshToken { get; init; }
}

public sealed record AuthTokensResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    DateTime ExpiresAt,
    UserSummary User);

public sealed record UserSummary(Guid Id, string Email, string Role);
