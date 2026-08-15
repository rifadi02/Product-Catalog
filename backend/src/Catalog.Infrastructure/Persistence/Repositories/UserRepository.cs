namespace Catalog.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    /// <summary>
    /// Looks a user up by an email that <see cref="Email.Create"/> has already trimmed and
    /// lower-cased. Because the stored value is normalised too, the SQL is a plain equality with
    /// no <c>lower(email)</c> wrapper — which is what keeps <c>ux_users_email</c> usable.
    /// </summary>
    public Task<User?> GetByEmailAsync(string normalisedEmail, CancellationToken ct = default)
    {
        var value = Email.FromTrusted(normalisedEmail);
        return db.Users.FirstOrDefaultAsync(u => u.Email == value, ct);
    }

    public Task<bool> EmailExistsAsync(string normalisedEmail, CancellationToken ct = default)
    {
        var value = Email.FromTrusted(normalisedEmail);
        return db.Users.AnyAsync(u => u.Email == value, ct);
    }

    public void Add(User user) => db.Users.Add(user);
}
