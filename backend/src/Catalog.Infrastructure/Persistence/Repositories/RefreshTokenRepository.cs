namespace Catalog.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    /// <summary>
    /// Tracked, and with the owning user eagerly loaded — the refresh path revokes this token
    /// and issues a replacement through <c>User.IssueRefreshTokenExpiringAt</c>, so both are
    /// needed in the same unit of work.
    /// </summary>
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(
        Guid userId, DateTime utcNow, CancellationToken ct = default) =>
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > utcNow)
            .ToListAsync(ct);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    /// <summary>
    /// Set-based delete: no entities are materialised, so pruning a million rows costs one
    /// statement rather than a million tracked objects.
    /// </summary>
    public Task<int> DeleteExpiredBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default) =>
        db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoffUtc)
            .ExecuteDeleteAsync(ct);
}
