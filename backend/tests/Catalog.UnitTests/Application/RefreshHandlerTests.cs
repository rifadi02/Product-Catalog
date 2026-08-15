namespace Catalog.UnitTests.Application;

public sealed class RefreshHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenFactory _factory = Substitute.For<IRefreshTokenFactory>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly User _user = User.Create(
        Email.Create("bob@example.com").Value, "hash", UserRole.User, Now);

    private RefreshHandler CreateHandler()
    {
        _clock.UtcNow.Returns(Now);
        _factory.HashOf("presented").Returns("presented-hash");
        _factory.Create().Returns(new RefreshTokenPair("new-plaintext", "new-hash"));
        _jwt.CreateAccessToken(Arg.Any<User>())
            .Returns(new AccessToken("access", Now.AddMinutes(15), 900));

        _users.GetByIdAsync(_user.Id).Returns(_user);

        return new RefreshHandler(_tokens, _users, _uow, _jwt, _factory, _clock,
            NullLogger<RefreshHandler>.Instance);
    }

    private static RefreshTokenRequest Request() => new() { RefreshToken = "presented" };

    private RefreshToken ActiveToken(TimeSpan? remaining = null) =>
        _user.IssueRefreshToken("presented-hash", Now, remaining ?? TimeSpan.FromDays(7));

    [Fact]
    public async Task An_unknown_token_is_rejected()
    {
        _tokens.GetByHashAsync("presented-hash").Returns((RefreshToken?)null);

        var result = await CreateHandler().HandleAsync(Request());

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("refresh_token_invalid");
    }

    [Fact]
    public async Task The_plaintext_token_is_never_used_as_a_lookup_key()
    {
        _tokens.GetByHashAsync(Arg.Any<string>()).Returns((RefreshToken?)null);

        await CreateHandler().HandleAsync(Request());

        await _tokens.Received(1).GetByHashAsync("presented-hash", Arg.Any<CancellationToken>());
        await _tokens.DidNotReceive().GetByHashAsync("presented", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        var token = ActiveToken(TimeSpan.FromMinutes(-1));
        _tokens.GetByHashAsync("presented-hash").Returns(token);

        var result = await CreateHandler().HandleAsync(Request());

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("refresh_token_invalid");
    }

    [Fact]
    public async Task A_valid_token_is_revoked_and_replaced_in_one_transaction()
    {
        var token = ActiveToken();
        _tokens.GetByHashAsync("presented-hash").Returns(token);

        var result = await CreateHandler().HandleAsync(Request());

        result.IsSuccess.Should().BeTrue();
        result.Value!.RefreshToken.Should().Be("new-plaintext");

        token.RevokedAt.Should().Be(Now);
        token.RevocationReason.Should().Be(RevocationReasons.Rotated);
        token.ReplacedByTokenHash.Should().Be("new-hash");

        _tokens.Received(1).Add(Arg.Is<RefreshToken>(t => t.TokenHash == "new-hash"));

        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rotation_inherits_the_original_absolute_expiry()
    {
        var token = ActiveToken();
        _tokens.GetByHashAsync("presented-hash").Returns(token);

        RefreshToken? issued = null;
        _tokens.When(t => t.Add(Arg.Any<RefreshToken>())).Do(c => issued = c.Arg<RefreshToken>());

        await CreateHandler().HandleAsync(Request());

        issued!.ExpiresAt.Should().Be(token.ExpiresAt);
    }

    [Fact]
    public async Task Presenting_an_already_rotated_token_revokes_the_whole_family()
    {
        var reused = ActiveToken();
        reused.Revoke(Now.AddMinutes(-5), RevocationReasons.Rotated, "some-hash");

        var sibling = _user.IssueRefreshToken("sibling-hash", Now, TimeSpan.FromDays(7));

        _tokens.GetByHashAsync("presented-hash").Returns(reused);
        _tokens.GetActiveForUserAsync(_user.Id, Now).Returns([sibling]);

        var result = await CreateHandler().HandleAsync(Request());

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("refresh_token_invalid");

        sibling.RevokedAt.Should().Be(Now);
        sibling.RevocationReason.Should().Be(RevocationReasons.ReuseDetected);

        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _tokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task Every_failure_returns_the_same_error()
    {
        var handler = CreateHandler();

        _tokens.GetByHashAsync("presented-hash").Returns((RefreshToken?)null);
        var unknown = await handler.HandleAsync(Request());

        _tokens.GetByHashAsync("presented-hash").Returns(ActiveToken(TimeSpan.FromMinutes(-1)));
        var expired = await handler.HandleAsync(Request());

        unknown.Error.Should().BeEquivalentTo(expired.Error);
    }
}
