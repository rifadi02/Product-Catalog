namespace Catalog.Api.Extensions;

public static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app, CancellationToken ct = default)
    {
        if (app.Environment.IsProduction() &&
            !app.Configuration.GetValue("Database:MigrateOnStartup", false))
        {
            app.Logger.LogInformation(
                "Skipping startup migrations in Production. Apply them as a deploy step: " +
                "dotnet ef migrations script --idempotent");
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var policy = Policy
            .Handle<Npgsql.NpgsqlException>()
            .WaitAndRetryAsync(5, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (ex, delay, attempt, _) => app.Logger.LogWarning(
                    ex, "Database not ready (attempt {Attempt}); retrying in {Delay}", attempt, delay));

        await policy.ExecuteAsync(_ => db.Database.MigrateAsync(ct), ct);

        app.Logger.LogInformation("Database schema is up to date.");
    }

    public static async Task SeedDatabaseAsync(this WebApplication app, CancellationToken ct = default)
    {
        if (!app.Configuration.GetValue("Seed:Enabled", false)) return;

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(ct);
    }
}
