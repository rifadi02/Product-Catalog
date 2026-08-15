namespace Catalog.Infrastructure.Persistence;

/// <summary>
/// §2.3.1. Lets <c>dotnet ef</c> construct a context without booting the whole application —
/// which matters because the app validates JWT configuration at startup and would otherwise
/// refuse to start just to emit a migration.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                   ?? "Host=localhost;Port=5432;Database=catalog;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", "public")
                .MigrationsAssembly(typeof(AppDbContextFactory).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
