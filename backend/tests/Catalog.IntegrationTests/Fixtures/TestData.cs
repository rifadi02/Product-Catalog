namespace Catalog.IntegrationTests.Fixtures;

/// <summary>
/// Arrangement helpers. Users are created directly against the database rather than through
/// <c>POST /auth/register</c>, because registration is rate-limited and because an Admin cannot
/// be created through the API at all — by design (§3.1 BR-2).
/// </summary>
internal static class TestData
{
    public const string DefaultPassword = "Str0ngPassw0rd!";

    public static async Task<User> CreateUserAsync(
        this CatalogApiFactory factory,
        string email,
        UserRole role = UserRole.User,
        string password = DefaultPassword)
    {
        await using var scope = factory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var user = User.Create(Email.Create(email).Value, hasher.Hash(password), role, clock.UtcNow);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    public static async Task<Product> AddProductAsync(this CatalogApiFactory factory, Product product)
    {
        await using var scope = factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product;
    }

    public static async Task<AuthTokensResponse> LoginAsync(
        this HttpClient client, string email, string password = DefaultPassword)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuthTokensResponse>())!;
    }

    /// <summary>Returns a client already carrying a bearer token for a freshly created user.</summary>
    public static async Task<HttpClient> AuthenticatedClientAsync(
        this CatalogApiFactory factory, string email, UserRole role = UserRole.User)
    {
        await factory.CreateUserAsync(email, role);

        var client = factory.CreateClient();
        var tokens = await client.LoginAsync(email);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return client;
    }
}
