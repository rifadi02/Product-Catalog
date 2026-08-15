namespace Catalog.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public const string IdentitySchema = "identity";
    public const string CatalogSchema = "catalog";

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresExtension("pg_trgm");

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(builder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(18, 2);
    }

}
