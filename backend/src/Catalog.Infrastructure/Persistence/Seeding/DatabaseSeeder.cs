namespace Catalog.Infrastructure.Persistence.Seeding;

/// <summary>
/// §2.4. A runtime seeder, not <c>HasData</c>: Bogus produces different values on every run, so
/// baking them into the migration snapshot would fill every subsequent
/// <c>dotnet ef migrations add</c> with UpdateData noise — and <c>HasData</c> needs explicit
/// primary keys, which <c>GENERATED ALWAYS AS IDENTITY</c> does not accept without leaving the
/// sequence out of step with the table.
/// </summary>
public sealed class DatabaseSeeder(
    AppDbContext db,
    IPasswordHasher hasher,
    IClock clock,
    ILogger<DatabaseSeeder> logger)
{
    private const int DeterministicSeed = 20260815;
    private const int ProductCount = 60;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        Randomizer.Seed = new Random(DeterministicSeed);

        var seededUsers = await SeedUsersAsync(ct);
        await SeedProductsAsync(seededUsers, ct);
    }

    private async Task<IReadOnlyList<Guid>> SeedUsersAsync(CancellationToken ct)
    {
        var wanted = new (string Email, string Password, UserRole Role)[]
        {
            ("admin@demo.local", "Admin#2026Demo", UserRole.Admin),
            ("user@demo.local",  "User#2026Demo",  UserRole.User)
        };

        var existing = await db.Users
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(ct);

        var byEmail = existing.ToDictionary(x => x.Email.Value, x => x.Id);
        var ids = new List<Guid>();

        foreach (var (email, password, role) in wanted)
        {
            if (byEmail.TryGetValue(email, out var existingId))
            {
                ids.Add(existingId);
                continue;
            }

            var user = User.Create(
                Email.Create(email).Value,
                hasher.Hash(password),
                role,
                clock.UtcNow);

            db.Users.Add(user);
            ids.Add(user.Id);
            logger.LogInformation("Seeded {Role} account {Email}", role, email);
        }

        await db.SaveChangesAsync(ct);
        return ids;
    }

    private async Task SeedProductsAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        if (await db.Products.IgnoreQueryFilters().AnyAsync(ct))
        {
            logger.LogInformation("Products already present — skipping product seed.");
            return;
        }

        var now = clock.UtcNow;

        var authorIds = userIds.ToArray();

        var faker = new Faker<Product>()
            .CustomInstantiator(f => Product.Create(
                name: BuildName(f),
                description: f.Commerce.ProductDescription(),
                price: Math.Round(f.Random.Decimal(4.99m, 2499.00m), 2),
                utcNow: f.Date.BetweenOffset(
                            now.AddDays(-180),
                            now.AddDays(-1)).UtcDateTime,
                createdBy: f.PickRandom(authorIds)));

        var products = faker.Generate(ProductCount);

        products.Add(Product.Create("Zero-Cost Sample Kit", null, 0.00m, now.AddDays(-3), userIds[0]));
        products.Add(Product.Create("Ultra Premium Flagship", new string('X', 2000), 999_999.99m, now.AddDays(-2), userIds[0]));
        products.Add(Product.Create("Café Latte Máquina — Ünïcode Test", "Accents, emoji ☕, and CJK 製品 in one name.", 349.50m, now.AddDays(-1), userIds[1]));

        var retired = Product.Create("Discontinued Widget", "Should not appear in any list.", 19.99m, now.AddDays(-90), userIds[0]);
        retired.SoftDelete(now.AddDays(-10));
        products.Add(retired);

        db.Products.AddRange(products);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded {Count} products ({Deleted} soft-deleted).",
            products.Count, products.Count(p => p.IsDeleted));
    }

    private static string BuildName(Faker f)
    {
        var name = $"{f.Commerce.ProductAdjective()} {f.Commerce.ProductMaterial()} {f.Commerce.Product()}";
        return name.Length > Product.NameMaxLength ? name[..Product.NameMaxLength] : name;
    }
}
