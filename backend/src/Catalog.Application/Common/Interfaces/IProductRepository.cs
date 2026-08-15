namespace Catalog.Application.Common.Interfaces;

/// <summary>
/// Repository Pattern (requirement F10). Deliberately intent-revealing rather than a generic
/// <c>IRepository&lt;T&gt;</c> with <c>IQueryable</c> leaking out of it: an <c>IQueryable</c>
/// return type means the persistence technology is still in the caller's hands, and the
/// abstraction has bought nothing.
/// </summary>
public interface IProductRepository
{
    /// <summary>Tracked entity for mutation paths. Honours the soft-delete query filter.</summary>
    Task<Product?> GetForUpdateAsync(int id, CancellationToken ct = default);

    /// <summary>Read path: projected DTO plus the <c>xmin</c> the ETag is derived from (§3.7 BR-4).</summary>
    Task<ProductSnapshot?> GetSnapshotAsync(int id, CancellationToken ct = default);

    Task<PagedResult<ProductDto>> ListAsync(ProductListSpec spec, CancellationToken ct = default);

    Task<PagedResult<ProductDto>> SearchAsync(ProductSearchSpec spec, CancellationToken ct = default);

    void Add(Product product);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<User?> GetByEmailAsync(string normalisedEmail, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(string normalisedEmail, CancellationToken ct = default);

    void Add(User user);
}

public interface IRefreshTokenRepository
{
    /// <summary>Looks up by SHA-256 hash and eagerly loads the owning user (§3.3 BR-1).</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Every not-yet-revoked, not-yet-expired token for a user — the reuse-detection blast radius.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveForUserAsync(Guid userId, DateTime utcNow, CancellationToken ct = default);

    void Add(RefreshToken token);

    /// <summary>Housekeeping (§4.6): drop tokens that expired before <paramref name="cutoffUtc"/>.</summary>
    Task<int> DeleteExpiredBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default);
}

/// <summary>
/// One transaction boundary shared by every repository, so that "revoke the old token and
/// issue the new one" is a single atomic <c>SaveChangesAsync</c> (§4.6).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
