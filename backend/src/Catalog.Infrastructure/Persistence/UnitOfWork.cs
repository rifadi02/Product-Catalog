namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// The transaction boundary. Every repository in a request shares one scoped
/// <see cref="AppDbContext"/>, so a single <c>SaveChangesAsync</c> commits all of their work
/// atomically — which is what makes "revoke the old refresh token and issue the new one"
/// crash-safe (§4.6).
/// </summary>
public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
