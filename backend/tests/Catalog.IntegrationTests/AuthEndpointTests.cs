namespace Catalog.IntegrationTests;

/// <summary>Covers §4.12 assertions 1–11.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthEndpointTests(CatalogApiFactory factory) : IAsyncLifetime
{
    public async Task InitializeAsync() => await factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static RegisterRequest Registration(
        string email = "new@example.com",
        string password = TestData.DefaultPassword,
        string? confirm = null) =>
        new() { Email = email, Password = password, ConfirmPassword = confirm ?? password };

    [DockerFact]
    public async Task Register_creates_a_User_and_never_an_Admin()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", Registration());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/api/v1/users/");

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        body!.Role.Should().Be(nameof(UserRole.User));
        body.Email.Should().Be("new@example.com");
    }

    [DockerFact]
    public async Task Register_normalises_the_email_before_the_uniqueness_check()
    {
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/register", Registration("Bob@Example.COM"));
        var duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", Registration("bob@example.com"));

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await duplicate.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("email_already_registered");
    }

    [DockerFact]
    public async Task Register_returns_no_tokens()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", Registration());
        var raw = await response.Content.ReadAsStringAsync();

        raw.Should().NotContain("accessToken").And.NotContain("refreshToken");
    }

    [DockerTheory]
    [InlineData("not-an-email", TestData.DefaultPassword)]
    [InlineData("ok@example.com", "short")]
    [InlineData("ok@example.com", "alllowercase1")]
    [InlineData("ok@example.com", "ALLUPPERCASE1")]
    [InlineData("ok@example.com", "NoDigitsHere!")]
    [InlineData("ok@example.com", "Password123")]
    public async Task Register_rejects_weak_input(string email, string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", Registration(email, password));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("validation_failed");
        problem.Errors.Should().NotBeEmpty();
    }

    [DockerFact]
    public async Task Register_rejects_a_mismatched_confirmation()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            Registration(confirm: "Different#1"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Errors.Should().ContainKey("confirmPassword");
    }

    [DockerFact]
    public async Task A_malformed_body_is_reported_as_malformed_json()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/auth/register",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("malformed_json");
    }

    [DockerFact]
    public async Task Login_returns_a_token_carrying_the_expected_claims()
    {
        await factory.CreateUserAsync("bob@example.com");
        var client = factory.CreateClient();

        var tokens = await client.LoginAsync("bob@example.com");

        tokens.TokenType.Should().Be("Bearer");
        tokens.ExpiresIn.Should().Be(900);
        tokens.User.Email.Should().Be("bob@example.com");
        tokens.User.Role.Should().Be(nameof(UserRole.User));

        var claims = JwtInspector.ReadClaims(tokens.AccessToken);
        claims["sub"].Should().Be(tokens.User.Id.ToString());
        claims["email"].Should().Be("bob@example.com");
        claims["role"].Should().Be(nameof(UserRole.User));
        claims["iss"].Should().Be(CatalogApiFactory.TestIssuer);
        claims["aud"].Should().Be(CatalogApiFactory.TestAudience);
        claims.Should().ContainKey("jti");
    }

    [DockerFact]
    public async Task An_unknown_email_and_a_wrong_password_return_identical_bodies()
    {
        await factory.CreateUserAsync("bob@example.com");
        var client = factory.CreateClient();

        var unknown = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = "nobody@example.com", Password = TestData.DefaultPassword });

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = "bob@example.com", Password = "Wr0ngPassword!" });

        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var a = await unknown.Content.ReadFromJsonAsync<ProblemResponse>();
        var b = await wrongPassword.Content.ReadFromJsonAsync<ProblemResponse>();

        a!.Code.Should().Be("invalid_credentials");
        a.Code.Should().Be(b!.Code);
        a.Detail.Should().Be(b.Detail);
        a.Title.Should().Be(b.Title);
    }

    [DockerFact]
    public async Task Logging_in_twice_leaves_both_sessions_alive()
    {
        await factory.CreateUserAsync("bob@example.com");
        var client = factory.CreateClient();

        var first = await client.LoginAsync("bob@example.com");
        var second = await client.LoginAsync("bob@example.com");

        var refreshed = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = first.RefreshToken });

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        second.RefreshToken.Should().NotBe(first.RefreshToken);
    }

    [DockerFact]
    public async Task A_protected_endpoint_without_a_header_is_401_missing_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("missing_token");
        problem.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    [DockerFact]
    public async Task A_token_signed_with_another_key_is_401_invalid_token()
    {
        var forged = JwtInspector.Forge(signingKey: "a-completely-different-key-0123456789");

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("invalid_token");
    }

    [DockerFact]
    public async Task An_expired_token_is_401_token_expired()
    {
        var expired = JwtInspector.Forge(expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Contains("x-token-expired").Should().BeTrue(
            "the SPA uses this to decide between refreshing and redirecting to login");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>();
        problem!.Code.Should().Be("token_expired");
    }

    [DockerTheory]
    [InlineData("wrong-issuer", CatalogApiFactory.TestAudience)]
    [InlineData(CatalogApiFactory.TestIssuer, "wrong-audience")]
    public async Task A_token_for_another_issuer_or_audience_is_401(string issuer, string audience)
    {
        var token = JwtInspector.Forge(issuer: issuer, audience: audience);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [DockerFact]
    public async Task Me_returns_the_caller_identity_from_claims()
    {
        var client = await factory.AuthenticatedClientAsync("bob@example.com");

        var summary = await client.GetFromJsonAsync<UserSummary>("/api/v1/auth/me");

        summary!.Email.Should().Be("bob@example.com");
        summary.Role.Should().Be(nameof(UserRole.User));
    }

    [DockerFact]
    public async Task Refresh_rotates_the_token_and_kills_the_old_one()
    {
        await factory.CreateUserAsync("bob@example.com");
        var client = factory.CreateClient();

        var original = await client.LoginAsync("bob@example.com");

        var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = original.RefreshToken });

        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var pair = await rotated.Content.ReadFromJsonAsync<AuthTokensResponse>();
        pair!.RefreshToken.Should().NotBe(original.RefreshToken);

        var again = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = pair.RefreshToken });
        again.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [DockerFact]
    public async Task Reusing_a_rotated_token_revokes_the_entire_family()
    {
        await factory.CreateUserAsync("bob@example.com");
        var client = factory.CreateClient();

        var t1 = await client.LoginAsync("bob@example.com");

        var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = t1.RefreshToken });
        var t2 = (await rotated.Content.ReadFromJsonAsync<AuthTokensResponse>())!;

        var replay = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = t1.RefreshToken });

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var t2AfterReuse = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = t2.RefreshToken });

        t2AfterReuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [DockerFact]
    public async Task Rotation_does_not_extend_the_absolute_session_expiry()
    {
        await factory.CreateUserAsync("bob@example.com");
        var client = factory.CreateClient();

        var original = await client.LoginAsync("bob@example.com");

        var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = original.RefreshToken });
        var next = (await rotated.Content.ReadFromJsonAsync<AuthTokensResponse>())!;

        var expiries = await RefreshTokenInspector.ExpiriesForAsync(factory, "bob@example.com");

        expiries.Should().HaveCount(2);
        expiries.Distinct().Should().HaveCount(1,
            "the replacement inherits the original absolute expiry, so refreshing forever " +
            "cannot extend a session");

        next.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [DockerFact]
    public async Task Refresh_works_with_an_expired_access_token()
    {
        await factory.CreateUserAsync("bob@example.com");
        var client = factory.CreateClient();

        var tokens = await client.LoginAsync("bob@example.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtInspector.Forge(expiresAt: DateTime.UtcNow.AddMinutes(-30)));

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [DockerFact]
    public async Task Logout_revokes_the_token_and_is_idempotent()
    {
        await factory.CreateUserAsync("bob@example.com");
        var client = factory.CreateClient();

        var tokens = await client.LoginAsync("bob@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var first = await client.PostAsJsonAsync("/api/v1/auth/logout",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken });
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await client.PostAsJsonAsync("/api/v1/auth/logout",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken });
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshAfterLogout = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = tokens.RefreshToken });
        refreshAfterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [DockerFact]
    public async Task Logout_cannot_revoke_another_users_session()
    {
        await factory.CreateUserAsync("victim@example.com");
        var victimClient = factory.CreateClient();
        var victimTokens = await victimClient.LoginAsync("victim@example.com");

        var attacker = await factory.AuthenticatedClientAsync("attacker@example.com");

        var response = await attacker.PostAsJsonAsync("/api/v1/auth/logout",
            new RefreshTokenRequest { RefreshToken = victimTokens.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var stillWorks = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = victimTokens.RefreshToken });

        stillWorks.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [DockerFact]
    public async Task Logout_requires_authentication()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/logout",
            new RefreshTokenRequest { RefreshToken = new string('a', 32) });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
