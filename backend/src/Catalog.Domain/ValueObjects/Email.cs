namespace Catalog.Domain.ValueObjects;

/// <summary>
/// A normalised email address. Normalisation (trim + lowercase, invariant culture) happens
/// once, here, so that uniqueness is a plain unique index on a plain varchar column and
/// "Bob@X.com" and "bob@x.com" can never both exist.
/// </summary>
public readonly partial record struct Email
{
    public const int MaxLength = 256;

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Result<Email>.Invalid("Email is required.");

        var normalised = input.Trim().ToLowerInvariant();

        if (normalised.Length > MaxLength)
            return Result<Email>.Invalid($"Email must not exceed {MaxLength} characters.");

        if (!EmailPattern().IsMatch(normalised))
            return Result<Email>.Invalid("Email is not a valid address.");

        return Result<Email>.Success(new Email(normalised));
    }

    /// <summary>Rehydration from a trusted source (the database). Skips validation.</summary>
    public static Email FromTrusted(string value) => new(value);

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s\.]+(\.[^@\s\.]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
