namespace Catalog.Api.Extensions;

public static class Policies
{
    public const string CanReadProducts = nameof(CanReadProducts);
    public const string CanWriteProducts = nameof(CanWriteProducts);
    public const string CanDeleteProducts = nameof(CanDeleteProducts);
    public const string Authenticated = nameof(Authenticated);
}

public static class AuthorizationExtensions
{
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services) =>
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())

            .AddPolicy(Policies.Authenticated, p => p.RequireAuthenticatedUser())

            .AddPolicy(Policies.CanReadProducts, p => p.RequireAssertion(_ => true))

            .AddPolicy(Policies.CanWriteProducts, p => p
                .RequireAuthenticatedUser()
                .RequireRole(nameof(UserRole.User), nameof(UserRole.Admin)))

            .AddPolicy(Policies.CanDeleteProducts, p => p
                .RequireAuthenticatedUser()
                .RequireRole(nameof(UserRole.Admin)))

            .Services;
}
