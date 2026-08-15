namespace Catalog.UnitTests.Application;

public sealed class LogoutHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IRefreshTokenFactory _factory = Substitute.For<IRefreshTokenFactory>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly User _owner = User.Create(
        Email.Create("bob@example.com").Value, "hash", UserRole.User, Now);

    private LogoutHandler CreateHandler()
    {
        _clock.UtcNow.Returns(Now);
        _factory.HashOf("presented").Returns("presented-hash");
        return new LogoutHandler(_tokens, _uow, _factory, _currentUser, _clock,
            NullLogger<LogoutHandler>.Instance);
    }

    private static RefreshTokenRequest Request() => new() { RefreshToken = "presented" };

    [Fact]
    public async Task The_owner_can_revoke_their_own_token()
    {
        var token = _owner.IssueRefreshToken("presented-hash", Now, TimeSpan.FromDays(7));
        _tokens.GetByHashAsync("presented-hash").Returns(token);
        _currentUser.UserId.Returns(_owner.Id);

        var result = await CreateHandler().HandleAsync(Request());

        result.IsSuccess.Should().BeTrue();
        token.RevokedAt.Should().Be(Now);
        token.RevocationReason.Should().Be(RevocationReasons.Logout);

        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_token_still_succeeds()
    {
        _tokens.GetByHashAsync("presented-hash").Returns((RefreshToken?)null);
        _currentUser.UserId.Returns(_owner.Id);

        var result = await CreateHandler().HandleAsync(Request());

        result.IsSuccess.Should().BeTrue();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logging_out_twice_is_a_no_op_the_second_time()
    {
        var token = _owner.IssueRefreshToken("presented-hash", Now, TimeSpan.FromDays(7));
        token.Revoke(Now.AddMinutes(-5), RevocationReasons.Logout);

        _tokens.GetByHashAsync("presented-hash").Returns(token);
        _currentUser.UserId.Returns(_owner.Id);

        var result = await CreateHandler().HandleAsync(Request());

        result.IsSuccess.Should().BeTrue();
        token.RevokedAt.Should().Be(Now.AddMinutes(-5), "Revoke is idempotent");
    }

    [Fact]
    public async Task A_token_belonging_to_someone_else_is_not_revoked()
    {
        var victimToken = _owner.IssueRefreshToken("presented-hash", Now, TimeSpan.FromDays(7));

        _tokens.GetByHashAsync("presented-hash").Returns(victimToken);
        _currentUser.UserId.Returns(Guid.CreateVersion7());

        var result = await CreateHandler().HandleAsync(Request());

        result.IsSuccess.Should().BeTrue();

        victimToken.RevokedAt.Should().BeNull("the victim's session must survive");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unidentifiable_caller_revokes_nothing()
    {
        var token = _owner.IssueRefreshToken("presented-hash", Now, TimeSpan.FromDays(7));
        _tokens.GetByHashAsync("presented-hash").Returns(token);
        _currentUser.UserId.Returns((Guid?)null);

        var result = await CreateHandler().HandleAsync(Request());

        result.IsSuccess.Should().BeTrue();
        token.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task The_plaintext_token_is_hashed_before_lookup()
    {
        _tokens.GetByHashAsync(Arg.Any<string>()).Returns((RefreshToken?)null);
        _currentUser.UserId.Returns(_owner.Id);

        await CreateHandler().HandleAsync(Request());

        await _tokens.Received(1).GetByHashAsync("presented-hash", Arg.Any<CancellationToken>());
        await _tokens.DidNotReceive().GetByHashAsync("presented", Arg.Any<CancellationToken>());
    }
}
