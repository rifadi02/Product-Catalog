namespace Catalog.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers every slice handler and validator. Handlers are scoped because they close over
    /// a scoped <c>DbContext</c> through their repositories.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), ServiceLifetime.Scoped);

        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<LogoutHandler>();

        services.AddScoped<CreateProductHandler>();
        services.AddScoped<UpdateProductHandler>();
        services.AddScoped<DeleteProductHandler>();
        services.AddScoped<GetProductByIdHandler>();
        services.AddScoped<ListProductsHandler>();
        services.AddScoped<SearchProductsHandler>();

        return services;
    }
}
