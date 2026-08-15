namespace Catalog.IntegrationTests.Fixtures;

/// <summary>
/// Reads refresh-token rows directly. Some invariants — "the replacement inherits the original
/// expiry", "the plaintext is never persisted" — are only observable in the database, because
/// the API deliberately never returns them.
/// </summary>
internal static class RefreshTokenInspector
{
    public static async Task<IReadOnlyList<DateTime>> ExpiriesForAsync(
        CatalogApiFactory factory, string email)
    {
        await using var scope = factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var value = Email.FromTrusted(email);

        return await db.RefreshTokens
            .Where(t => db.Users.Any(u => u.Id == t.UserId && u.Email == value))
            .Select(t => t.ExpiresAt)
            .ToListAsync();
    }

    public static async Task<IReadOnlyList<string>> StoredHashesAsync(CatalogApiFactory factory)
    {
        await using var scope = factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.RefreshTokens.Select(t => t.TokenHash).ToListAsync();
    }
}
