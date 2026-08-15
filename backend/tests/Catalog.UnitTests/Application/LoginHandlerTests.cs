namespace Catalog.UnitTests.Application;

/// <summary>
/// These run with no database, no web host and no container — which is the point of handlers
/// depending on interfaces rather than on EF and HttpContext.
/// </summary>
public sealed class LoginHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _tokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenFactory _refresh = Substitute.For<IRefreshTokenFactory>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private LoginHandler CreateHandler()
    {
        _clock.UtcNow.Returns(Now);
        _jwt.CreateAccessToken(Arg.Any<User>())
            .Returns(new AccessToken("access-token", Now.AddMinutes(15), 900));
        _refresh.Create().Returns(new RefreshTokenPair("plaintext-token", "hashed-token"));

        var options = Options.Create(new JwtOptions
        {
            Issuer = "test",
            Audience = "test",
            SigningKey = new string('k', 32)
        });

        return new LoginHandler(_users, _tokens, _uow, _hasher, _jwt, _refresh, _clock,
            options, NullLogger<LoginHandler>.Instance);
    }

    private static User ExistingUser() =>
        User.Create(Email.Create("bob@example.com").Value, "stored-hash", UserRole.User, Now);

    private static LoginRequest Request(string email = "bob@example.com", string password = "Correct#1") =>
        new() { Email = email, Password = password };

    [Fact]
    public async Task Valid_credentials_return_a_token_pair()
    {
        var user = ExistingUser();
        _users.GetByEmailAsync("bob@example.com").Returns(user);
        _hasher.Verify("Correct#1", "stored-hash").Returns(true);

        var result = await CreateHandler().HandleAsync(Request(), sourceIp: "127.0.0.1");

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be("access-token");
        result.Value.TokenType.Should().Be("Bearer");
        result.Value.ExpiresIn.Should().Be(900);
        result.Value.User.Email.Should().Be("bob@example.com");
        result.Value.User.Role.Should().Be("User");
    }

    [Fact]
    public async Task Successful_login_returns_the_plaintext_refresh_token_but_stores_only_its_hash()
    {
        var user = ExistingUser();
        _users.GetByEmailAsync("bob@example.com").Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var result = await CreateHandler().HandleAsync(Request(), null);

        result.Value!.RefreshToken.Should().Be("plaintext-token");

        _tokens.Received(1).Add(Arg.Is<RefreshToken>(t => t.TokenHash == "hashed-token"));
    }

    [Fact]
    public async Task Successful_login_stamps_LastLoginAt_and_commits_once()
    {
        var user = ExistingUser();
        _users.GetByEmailAsync(Arg.Any<string>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await CreateHandler().HandleAsync(Request(), null);

        user.LastLoginAt.Should().Be(Now);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_email_still_runs_a_hash_verification()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        var result = await CreateHandler().HandleAsync(Request(email: "nobody@example.com"), null);

        result.IsSuccess.Should().BeFalse();
        _hasher.Received(1).BurnTime("Correct#1");
    }

    [Fact]
    public async Task A_structurally_invalid_email_also_burns_time_and_returns_401_not_400()
    {
        var result = await CreateHandler().HandleAsync(Request(email: "not-an-address"), null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(Catalog.Domain.Common.ErrorKind.Unauthorized);
        _hasher.Received(1).BurnTime(Arg.Any<string>());
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_produce_identical_errors()
    {
        _users.GetByEmailAsync("nobody@example.com").Returns((User?)null);
        _users.GetByEmailAsync("bob@example.com").Returns(ExistingUser());
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var handler = CreateHandler();

        var unknownEmail = await handler.HandleAsync(Request(email: "nobody@example.com"), null);
        var wrongPassword = await handler.HandleAsync(Request(), null);

        unknownEmail.Error.Should().BeEquivalentTo(wrongPassword.Error);
        unknownEmail.Error!.Code.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task A_failed_login_writes_nothing()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        await CreateHandler().HandleAsync(Request(), null);

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _tokens.DidNotReceive().Add(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task The_email_is_normalised_before_lookup()
    {
        _users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        await CreateHandler().HandleAsync(Request(email: "  BOB@Example.COM "), null);

        await _users.Received(1).GetByEmailAsync("bob@example.com", Arg.Any<CancellationToken>());
    }
}
