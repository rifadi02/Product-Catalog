namespace Catalog.Domain.Entities;

public sealed class User
{
    private readonly List<RefreshToken> _refreshTokens = [];

    private User() { }

    public Guid Id { get; private set; }
    public Email Email { get; private set; }
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public static User Create(Email email, string passwordHash, UserRole role, DateTime utcNow)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = utcNow
        };

    public void RecordLogin(DateTime utcNow) => LastLoginAt = utcNow;

    public RefreshToken IssueRefreshToken(string tokenHash, DateTime utcNow, TimeSpan lifetime)
    {
        var token = RefreshToken.Issue(Id, tokenHash, utcNow, lifetime);
        _refreshTokens.Add(token);
        return token;
    }

    /// <summary>
    /// Rotation overload (§4.6). The replacement token inherits an <b>absolute</b> expiry from the
    /// token it replaces, so refreshing forever cannot extend a session past its original 7 days.
    /// </summary>
    public RefreshToken IssueRefreshTokenExpiringAt(string tokenHash, DateTime utcNow, DateTime expiresAt)
    {
        var token = RefreshToken.Issue(Id, tokenHash, utcNow, expiresAt - utcNow);
        _refreshTokens.Add(token);
        return token;
    }
}
