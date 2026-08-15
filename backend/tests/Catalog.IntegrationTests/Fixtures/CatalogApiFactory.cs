namespace Catalog.IntegrationTests.Fixtures;

/// <summary>
/// The whole API against a real PostgreSQL, started once per test collection.
///
/// Testcontainers rather than an in-memory provider, because the things worth testing here are
/// the things an in-memory provider does not have: the soft-delete query filter compiled to real
/// SQL, <c>ILIKE</c> escaping, the unique index that catches a registration race, and the
/// <c>xmin</c> concurrency token. A test that passes against a fake database and fails against
/// Postgres is worse than no test.
/// </summary>
public sealed class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("catalog_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>A 32-byte key, because HS256 rejects anything shorter — as it should.</summary>
    public const string TestSigningKey = "integration-test-signing-key-0123456789";
    public const string TestIssuer = "product-catalog-api-tests";
    public const string TestAudience = "product-catalog-spa-tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),

                ["Redis:Connection"] = "",

                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["Jwt:SigningKey"] = TestSigningKey,
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",

                ["Database:MigrateOnStartup"] = "true",

                ["Seed:Enabled"] = "false"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestClientAddressStartupFilter>();

            services.RemoveAll<IDistributedCache>();
            services.AddSingleton<ResettableDistributedCache>();
            services.AddSingleton<IDistributedCache>(
                sp => sp.GetRequiredService<ResettableDistributedCache>());
        });
    }

    /// <summary>
    /// A client that the rate limiter sees as its own caller. Each one gets a fresh identity, so
    /// one test's login attempts cannot exhaust another's budget.
    /// </summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);

        client.DefaultRequestHeaders.Add(
            TestClientAddressStartupFilter.HeaderName, Guid.NewGuid().ToString());
    }

    /// <summary>
    /// A client sharing a caller identity with every other client built from the same
    /// <paramref name="clientId"/> — the way to assert on rate limiting rather than sidestep it.
    /// </summary>
    public HttpClient CreateClientAs(string clientId)
    {
        var client = CreateClient();

        client.DefaultRequestHeaders.Remove(TestClientAddressStartupFilter.HeaderName);
        client.DefaultRequestHeaders.Add(TestClientAddressStartupFilter.HeaderName, clientId);

        return client;
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        AssertPointedAtTheContainer();
    }

    /// <summary>
    /// Fails loudly if the API resolved a different database than the one this fixture started.
    ///
    /// <para>This suite was once green for the wrong reason. Composition read its connection
    /// string eagerly, before <see cref="ConfigureWebHost"/>'s overrides were layered on, so the
    /// API quietly connected to the <c>localhost:5432</c> in <c>appsettings.json</c> — a
    /// developer's Compose database. The tests passed whenever that happened to be running, which
    /// meant <see cref="ResetDatabaseAsync"/> was issuing <c>TRUNCATE</c> against a development
    /// catalogue, and failed with a connection error whenever it was not. Either way the container
    /// beside it was untouched.</para>
    ///
    /// <para>A green suite has to mean something, so the fixture now checks the claim it depends
    /// on instead of assuming it.</para>
    /// </summary>
    private void AssertPointedAtTheContainer()
    {
        var expected = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString());

        using var scope = Services.CreateScope();
        var actual = new NpgsqlConnectionStringBuilder(
            scope.ServiceProvider.GetRequiredService<AppDbContext>()
                 .Database.GetConnectionString());

        if (actual.Port == expected.Port && actual.Database == expected.Database)
            return;

        throw new InvalidOperationException(
            $"The API is configured against {actual.Host}:{actual.Port}/{actual.Database}, but the " +
            $"test container is {expected.Host}:{expected.Port}/{expected.Database}. The test " +
            "configuration is not reaching composition — something is reading IConfiguration at " +
            "service-registration time instead of at resolution time. Refusing to run, because " +
            "these tests TRUNCATE every table.");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>A scoped context for arranging state directly, bypassing HTTP.</summary>
    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    /// <summary>
    /// Truncates every table so each test starts from a known empty database. Cheaper than
    /// recreating the schema, and it resets the identity sequence so product ids are predictable.
    ///
    /// <para>The cache is emptied in the same breath. Resetting one without the other leaves the
    /// API answering from entries that describe rows the next test never created — and because
    /// <c>RESTART IDENTITY</c> reissues the same ids, those entries are addressed by keys the next
    /// test will genuinely ask for.</para>
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE catalog.products RESTART IDENTITY CASCADE;");
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE identity.refresh_tokens, identity.users RESTART IDENTITY CASCADE;");

        Services.GetRequiredService<ResettableDistributedCache>().Clear();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<CatalogApiFactory>
{
    public const string Name = "catalog-api";
}
