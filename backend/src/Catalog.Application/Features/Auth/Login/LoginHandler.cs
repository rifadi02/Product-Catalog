namespace Catalog.Application.Features.Auth.Login;

/// <summary>§3.2 — <c>POST /api/v1/auth/login</c>.</summary>
public sealed class LoginHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork uow,
    IPasswordHasher hasher,
    IJwtTokenService jwt,
    IRefreshTokenFactory refreshTokenFactory,
    IClock clock,
    IOptions<JwtOptions> jwtOptions,
    ILogger<LoginHandler> logger)
{
    private static Error InvalidCredentials => Error.Unauthorized(
        "invalid_credentials", "Email or password is incorrect.");

    public async Task<Result<AuthTokensResponse>> HandleAsync(
        LoginRequest request, string? sourceIp, CancellationToken ct = default)
    {
        var email = Email.Create(request.Email);
        if (!email.IsSuccess)
        {
            hasher.BurnTime(request.Password);
            return Result<AuthTokensResponse>.Failure(InvalidCredentials);
        }

        var user = await users.GetByEmailAsync(email.Value.Value, ct);

        if (user is null)
        {
            hasher.BurnTime(request.Password);
            LogFailure(email.Value.Value, sourceIp);
            return Result<AuthTokensResponse>.Failure(InvalidCredentials);
        }

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            LogFailure(email.Value.Value, sourceIp);
            return Result<AuthTokensResponse>.Failure(InvalidCredentials);
        }

        var now = clock.UtcNow;
        var options = jwtOptions.Value;

        var access = jwt.CreateAccessToken(user);
        var refresh = refreshTokenFactory.Create();

        var token = user.IssueRefreshToken(refresh.Hash, now, options.RefreshTokenLifetime);
        refreshTokens.Add(token);

        user.RecordLogin(now);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} logged in", user.Id);

        return Result<AuthTokensResponse>.Success(new AuthTokensResponse(
            AccessToken: access.Value,
            RefreshToken: refresh.Plaintext,
            TokenType: "Bearer",
            ExpiresIn: access.ExpiresInSeconds,
            ExpiresAt: access.ExpiresAtUtc,
            User: new UserSummary(user.Id, user.Email.Value, user.Role.ToString())));
    }

    private void LogFailure(string email, string? sourceIp) =>
        logger.LogWarning("Failed login attempt for {Email} from {SourceIp}", email, sourceIp ?? "unknown");
}
