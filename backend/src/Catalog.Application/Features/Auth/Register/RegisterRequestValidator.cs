namespace Catalog.Application.Features.Auth.Register;

/// <summary>
/// The rules Data Annotations cannot express without a fragile regex (§3.1 validation table).
/// Runs alongside the annotations, not instead of them — the requirement asks for Data
/// Annotations, and this covers what they structurally cannot reach.
/// </summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "passw0rd", "12345678", "123456789",
        "qwerty123", "letmein1", "welcome1", "admin123", "iloveyou1", "abc12345",
        "monkey123", "dragon123", "football1", "baseball1", "sunshine1", "princess1"
    };

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Password)
            .Must(p => p.Any(char.IsUpper))
                .WithMessage("Password must contain at least one uppercase letter.")
            .Must(p => p.Any(char.IsLower))
                .WithMessage("Password must contain at least one lowercase letter.")
            .Must(p => p.Any(char.IsDigit))
                .WithMessage("Password must contain at least one digit.")
            .Must(p => !CommonPasswords.Contains(p))
                .WithMessage("Password is too common. Choose something less guessable.")
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}
