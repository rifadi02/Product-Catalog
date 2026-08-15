namespace Catalog.Infrastructure;

/// <summary>
/// Composition for the infrastructure layer.
///
/// <para><b>Nothing here reads configuration at registration time.</b> Every value is pulled from
/// the <see cref="IConfiguration"/> in the container, at the moment the service is resolved. That
/// is not a stylistic preference — under the minimal hosting model a host builder can still gain
/// configuration sources after <c>Program.cs</c> has finished running (this is exactly what
/// <c>WebApplicationFactory.ConfigureWebHost</c> does, and it is how the integration tests point
/// the API at their Testcontainers Postgres). A registration that captures
/// <c>configuration["…"]</c> into a local silently keeps the pre-override value, so the app
/// connects somewhere nobody asked for and the tests pass or fail for the wrong reason.</para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddPersistence();
        services.AddCaching();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IRefreshTokenFactory, RefreshTokenFactory>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddScoped<DatabaseSeeder>();
        services.AddHostedService<ExpiredRefreshTokenCleanupService>();

        return services;
    }

    /// <summary>
    /// The connection string the application is actually using, read from the container's
    /// configuration. Shared by the <c>DbContext</c> and the readiness health check so the two can
    /// never disagree about which database "the database" means.
    /// </summary>
    public static string GetCatalogConnectionString(this IServiceProvider services) =>
        services.GetRequiredService<IConfiguration>().GetCatalogConnectionString();

    /// <inheritdoc cref="GetCatalogConnectionString(IServiceProvider)"/>
    public static string GetCatalogConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:Default is missing. Copy .env.example to .env, or set " +
            "ConnectionStrings__Default in the environment.");

    private static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(sp.GetCatalogConnectionString(), npg =>
            {
                npg.MigrationsHistoryTable("__ef_migrations_history", "public");
                npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);

                npg.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            var env = sp.GetRequiredService<IHostEnvironment>();
            if (env.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    /// <summary>
    /// Redis when a connection string is configured, in-process otherwise.
    ///
    /// <para>Both sit behind <see cref="ICacheService"/> and the same <c>IDistributedCache</c>
    /// contract, so the fallback is a genuine fallback and not a different code path: the tests
    /// and a laptop without Docker exercise the same caching logic the container does.</para>
    ///
    /// <para>The choice is made inside the <c>IDistributedCache</c> factory rather than by calling
    /// <c>AddStackExchangeRedisCache</c> or <c>AddDistributedMemoryCache</c> here, because those
    /// would force the <c>Redis:Connection</c> lookup to happen at registration time — see the
    /// note on the class.</para>
    /// </summary>
    private static IServiceCollection AddCaching(this IServiceCollection services)
    {
        services.AddOptions();
        services.AddLogging();

        services.AddOptions<RedisCacheOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.Configuration = configuration.GetRedisConnection();
                options.InstanceName = "catalog:";
            });

        services.AddSingleton<IDistributedCache>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();

            return configuration.UsesRedis()
                ? new RedisCache(sp.GetRequiredService<IOptions<RedisCacheOptions>>())
                : new MemoryDistributedCache(
                      sp.GetRequiredService<IOptions<MemoryDistributedCacheOptions>>(),
                      sp.GetRequiredService<ILoggerFactory>());
        });

        services.AddSingleton(sp => new CacheModeReport(
            sp.GetRequiredService<IConfiguration>().UsesRedis() ? "redis" : "in-memory"));

        services.AddScoped<ICacheService, DistributedCacheService>();

        return services;
    }

    /// <summary>The configured Redis endpoint, or <c>null</c> when caching should stay in-process.</summary>
    public static string? GetRedisConnection(this IConfiguration configuration)
    {
        var connection = configuration.GetSection("Redis")["Connection"];
        return string.IsNullOrWhiteSpace(connection) ? null : connection;
    }

    /// <inheritdoc cref="GetRedisConnection"/>
    public static bool UsesRedis(this IConfiguration configuration) =>
        configuration.GetRedisConnection() is not null;
}

/// <summary>Which cache backend composition selected. Logged once at startup so the answer to
/// "is Redis actually being used?" is in the first ten lines of the log, not a guess.</summary>
public sealed record CacheModeReport(string Mode)
{
    public void Log(ILogger logger) => logger.LogInformation("Cache backend: {CacheMode}", Mode);
}
