namespace Catalog.UnitTests.Api;

public sealed class AuthControllerTests
{
    private static readonly Guid Caller = Guid.CreateVersion7();

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenFactory _refreshTokenFactory = Substitute.For<IRefreshTokenFactory>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _clock.UtcNow.Returns(ControllerTestContext.Now);
        _controller = new AuthController(_currentUser);
    }

    private RegisterHandler RegisterHandler() =>
        new(_users, _uow, _hasher, _clock, NullLogger<RegisterHandler>.Instance);

    private LogoutHandler LogoutHandler() =>
        new(_refreshTokens, _uow, _refreshTokenFactory, _currentUser, _clock,
            NullLogger<LogoutHandler>.Instance);

    private static RegisterRequest Registration(string email = "new@example.com") =>
        new() { Email = email, Password = "Str0ngPassw0rd!", ConfirmPassword = "Str0ngPassw0rd!" };

    // ── POST /auth/register ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_returns_201_with_a_Location_for_the_new_user()
    {
        _controller.WithHttpContext();
        _users.EmailExistsAsync(Arg.Any<string>()).Returns(false);
        _hasher.Hash(Arg.Any<string>()).Returns("hashed");

        var result = await _controller.Register(Registration(), RegisterHandler(), default);

        var created = result.Should().BeOfType<CreatedResult>().Subject;

        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Location.Should().StartWith("/api/v1/users/");

        created.Value.Should().BeOfType<RegisterResponse>()
               .Which.Role.Should().Be(nameof(UserRole.User),
                   "self-registration must never mint an Admin");
    }

    [Fact]
    public async Task Register_maps_a_duplicate_email_to_a_409_problem_document()
    {
        _controller.WithHttpContext();
        _users.EmailExistsAsync(Arg.Any<string>()).Returns(true);

        var result = await _controller.Register(Registration(), RegisterHandler(), default);

        ControllerTestContext.StatusOf(result).Should().Be(StatusCodes.Status409Conflict);
        ControllerTestContext.CodeOf(result).Should().Be("email_already_registered");
    }

    [Fact]
    public async Task Register_never_echoes_a_token()
    {
        _controller.WithHttpContext();
        _users.EmailExistsAsync(Arg.Any<string>()).Returns(false);
        _hasher.Hash(Arg.Any<string>()).Returns("hashed");

        var result = await _controller.Register(Registration(), RegisterHandler(), default);

        var body = JsonSerializer.Serialize(
            result.Should().BeOfType<CreatedResult>().Subject.Value);

        body.Should().NotContain("ccessToken").And.NotContain("efreshToken",
            "registration authenticates nobody; issuing a token here would skip the login path");
    }

    // ── POST /auth/login ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_passes_the_caller_address_through_to_the_audit_log()
    {
        var http = _controller.WithHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.7");

        var logger = new CapturingLogger<LoginHandler>();
        _users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        var result = await _controller.Login(
            new LoginRequest { Email = "someone@example.com", Password = "whatever" },
            new LoginHandler(_users, _refreshTokens, _uow, _hasher, _jwt, _refreshTokenFactory,
                _clock, Options.Create(JwtSettings()), logger),
            default);

        ControllerTestContext.StatusOf(result).Should().Be(StatusCodes.Status401Unauthorized);
        ControllerTestContext.CodeOf(result).Should().Be("invalid_credentials");

        logger.Messages.Should().ContainSingle()
              .Which.Should().Contain("198.51.100.7",
                  "a controller that never read Connection would log every brute-force attempt " +
                  "as coming from nowhere");
    }

    [Fact]
    public async Task Login_records_an_unattributable_attempt_rather_than_failing()
    {
        _controller.WithHttpContext();   // no RemoteIpAddress, as with a unix socket or a bad proxy

        var logger = new CapturingLogger<LoginHandler>();
        _users.GetByEmailAsync(Arg.Any<string>()).Returns((User?)null);

        await _controller.Login(
            new LoginRequest { Email = "someone@example.com", Password = "whatever" },
            new LoginHandler(_users, _refreshTokens, _uow, _hasher, _jwt, _refreshTokenFactory,
                _clock, Options.Create(JwtSettings()), logger),
            default);

        logger.Messages.Should().ContainSingle().Which.Should().Contain("unknown");
    }

    private static JwtOptions JwtSettings() => new()
    {
        Issuer = "product-catalog-api-tests",
        Audience = "product-catalog-spa-tests",
        SigningKey = "unit-test-signing-key-0123456789abcdef",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    };

    // ── POST /auth/logout ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_returns_204_when_the_token_is_already_gone()
    {
        _controller.WithHttpContext();
        _currentUser.UserId.Returns(Caller);
        _refreshTokenFactory.HashOf(Arg.Any<string>()).Returns("hash");
        _refreshTokens.GetByHashAsync("hash").Returns((RefreshToken?)null);

        var result = await _controller.Logout(
            new RefreshTokenRequest { RefreshToken = new string('t', 32) },
            LogoutHandler(), default);

        result.Should().BeOfType<NoContentResult>(
            "logout is idempotent; a second call is not an error");
    }

    // ── GET /auth/me ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Me_reports_the_identity_carried_by_the_token()
    {
        _controller.WithHttpContext();
        _currentUser.UserId.Returns(Caller);
        _currentUser.Email.Returns("someone@example.com");
        _currentUser.Role.Returns(UserRole.Admin);

        var summary = _controller.Me().Should().BeOfType<OkObjectResult>()
                                 .Which.Value.Should().BeOfType<UserSummary>().Subject;

        summary.Id.Should().Be(Caller);
        summary.Email.Should().Be("someone@example.com");
        summary.Role.Should().Be(nameof(UserRole.Admin),
            "the SPA gates its admin UI on this string");
    }

    [Fact]
    public void Me_reports_blank_fields_rather_than_null_for_a_sparse_token()
    {
        _controller.WithHttpContext();
        _currentUser.UserId.Returns(Caller);
        _currentUser.Email.Returns((string?)null);
        _currentUser.Role.Returns((UserRole?)null);

        var summary = _controller.Me().Should().BeOfType<OkObjectResult>()
                                 .Which.Value.Should().BeOfType<UserSummary>().Subject;

        summary.Id.Should().Be(Caller);
        summary.Email.Should().BeEmpty();
        summary.Role.Should().BeEmpty(
            "UserSummary declares non-nullable strings; a null here would break the SPA's parser");
    }

    [Fact]
    public void Me_rejects_a_token_with_no_usable_subject_claim()
    {
        _controller.WithHttpContext();
        _currentUser.UserId.Returns((Guid?)null);

        var result = _controller.Me();

        ControllerTestContext.StatusOf(result).Should().Be(StatusCodes.Status401Unauthorized);
        ControllerTestContext.CodeOf(result).Should().Be("invalid_token",
            "a token that authenticated but carries no subject is not a 200 with a blank user");
    }
}
