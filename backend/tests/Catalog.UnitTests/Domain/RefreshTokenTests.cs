namespace Catalog.UnitTests.Domain;

public sealed class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.CreateVersion7();

    private static RefreshToken Issue(TimeSpan? lifetime = null) =>
        RefreshToken.Issue(UserId, "hash", Now, lifetime ?? TimeSpan.FromDays(7));

    [Fact]
    public void A_fresh_token_is_active()
    {
        var token = Issue();

        token.StatusAt(Now).Should().Be(RefreshTokenStatus.Active);
        token.IsActiveAt(Now).Should().BeTrue();
        token.ExpiresAt.Should().Be(Now.AddDays(7));
    }

    [Fact]
    public void A_token_is_expired_at_the_instant_it_expires()
    {
        var token = Issue(TimeSpan.FromMinutes(15));

        token.StatusAt(Now.AddMinutes(15)).Should().Be(RefreshTokenStatus.Expired);
        token.StatusAt(Now.AddMinutes(14).AddSeconds(59)).Should().Be(RefreshTokenStatus.Active);
    }

    [Fact]
    public void Revocation_wins_over_expiry()
    {
        var token = Issue();
        token.Revoke(Now.AddHours(1), "logout");

        token.StatusAt(Now.AddDays(30)).Should().Be(RefreshTokenStatus.Revoked);
    }

    [Fact]
    public void Revoke_records_the_reason_and_the_replacement()
    {
        var token = Issue();
        var at = Now.AddHours(2);

        token.Revoke(at, RevocationReasons.Rotated, "next-hash");

        token.RevokedAt.Should().Be(at);
        token.RevocationReason.Should().Be("rotated");
        token.ReplacedByTokenHash.Should().Be("next-hash");
    }

    [Fact]
    public void Revoke_is_idempotent()
    {
        var token = Issue();
        var first = Now.AddHours(1);

        token.Revoke(first, RevocationReasons.Rotated, "hash-2");
        token.Revoke(Now.AddHours(5), RevocationReasons.ReuseDetected, "hash-3");

        token.RevokedAt.Should().Be(first);
        token.RevocationReason.Should().Be("rotated");
        token.ReplacedByTokenHash.Should().Be("hash-2");
    }

    [Fact]
    public void Rotation_does_not_extend_the_original_expiry()
    {
        var user = TestUser();
        var original = user.IssueRefreshToken("hash-1", Now, TimeSpan.FromDays(7));

        var rotatedAt = Now.AddDays(6);
        var replacement = user.IssueRefreshTokenExpiringAt("hash-2", rotatedAt, original.ExpiresAt);

        replacement.ExpiresAt.Should().Be(original.ExpiresAt);
        replacement.CreatedAt.Should().Be(rotatedAt);
    }

    private static User TestUser() =>
        User.Create(
            Catalog.Domain.ValueObjects.Email.Create("bob@example.com").Value,
            "hash",
            UserRole.User,
            Now);
}
