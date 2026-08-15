namespace Catalog.Application.Features.Auth.Logout;

/// <summary>§3.4 — <c>POST /api/v1/auth/logout</c>. Always 204.</summary>
public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork uow,
    IRefreshTokenFactory refreshTokenFactory,
    ICurrentUser currentUser,
    IClock clock,
    ILogger<LogoutHandler> logger)
{
    public async Task<Result> HandleAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var token = await refreshTokens.GetByHashAsync(
            refreshTokenFactory.HashOf(request.RefreshToken), ct);

        if (token is null)
            return Result.Success();

        if (currentUser.UserId is null || token.UserId != currentUser.UserId)
        {
            logger.LogWarning(
                "Logout presented a refresh token owned by {OwnerId} while authenticated as {CallerId}",
                token.UserId, currentUser.UserId);
            return Result.Success();
        }

        token.Revoke(clock.UtcNow, RevocationReasons.Logout);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} logged out", token.UserId);

        return Result.Success();
    }
}
