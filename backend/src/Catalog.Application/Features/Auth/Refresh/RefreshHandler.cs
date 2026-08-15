namespace Catalog.Application.Features.Auth.Refresh;

/// <summary>§3.3 / §4.6 — <c>POST /api/v1/auth/refresh</c>. Rotation with reuse detection.</summary>
public sealed class RefreshHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IUnitOfWork uow,
    IJwtTokenService jwt,
    IRefreshTokenFactory refreshTokenFactory,
    IClock clock,
    ILogger<RefreshHandler> logger)
{
    private static Error Invalid => Error.Unauthorized(
        "refresh_token_invalid", "The refresh token is invalid or has expired.");

    public async Task<Result<AuthTokensResponse>> HandleAsync(
        RefreshTokenRequest request, CancellationToken ct = default)
    {
        var presentedHash = refreshTokenFactory.HashOf(request.RefreshToken);

        var existing = await refreshTokens.GetByHashAsync(presentedHash, ct);
        if (existing is null)
            return Result<AuthTokensResponse>.Failure(Invalid);

        var now = clock.UtcNow;

        if (existing.RevokedAt is not null)
        {
            logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoking all active tokens",
                existing.UserId);

            var active = await refreshTokens.GetActiveForUserAsync(existing.UserId, now, ct);
            foreach (var token in active)
                token.Revoke(now, RevocationReasons.ReuseDetected);

            await uow.SaveChangesAsync(ct);
            return Result<AuthTokensResponse>.Failure(Invalid);
        }

        if (existing.ExpiresAt <= now)
            return Result<AuthTokensResponse>.Failure(Invalid);

        var user = existing.User ?? await users.GetByIdAsync(existing.UserId, ct);
        if (user is null)
            return Result<AuthTokensResponse>.Failure(Invalid);

        var replacement = refreshTokenFactory.Create();

        existing.Revoke(now, RevocationReasons.Rotated, replacement.Hash);

        var issued = user.IssueRefreshTokenExpiringAt(replacement.Hash, now, existing.ExpiresAt);
        refreshTokens.Add(issued);

        await uow.SaveChangesAsync(ct);

        var access = jwt.CreateAccessToken(user);

        logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);

        return Result<AuthTokensResponse>.Success(new AuthTokensResponse(
            AccessToken: access.Value,
            RefreshToken: replacement.Plaintext,
            TokenType: "Bearer",
            ExpiresIn: access.ExpiresInSeconds,
            ExpiresAt: access.ExpiresAtUtc,
            User: new UserSummary(user.Id, user.Email.Value, user.Role.ToString())));
    }
}
