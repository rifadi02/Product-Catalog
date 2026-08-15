namespace Catalog.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddAppSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Product Catalog API",
                Version = "v1",
                Description =
                    "Product catalogue with JWT authentication, role-based authorization, " +
                    "cache-aside reads and soft deletes.\n\n" +
                    "**Errors** — every non-2xx response is an RFC 7807 `application/problem+json` " +
                    "document carrying a stable `code` and a `traceId`. Switch on `code`, not on `title`.\n\n" +
                    "**Paging** — `pageSize` is clamped server-side to 100. Asking for more returns 100, not an error.\n\n" +
                    "**Demo accounts** — `admin@demo.local` / `Admin#2026Demo` and " +
                    "`user@demo.local` / `User#2026Demo` when seeding is enabled."
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the raw access token from POST /api/v1/auth/login. " +
                              "Swagger adds the 'Bearer ' prefix for you.",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            options.AddSecurityDefinition("Bearer", scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });

            var xml = Path.Combine(AppContext.BaseDirectory,
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xml))
                options.IncludeXmlComments(xml, includeControllerXmlComments: true);

            foreach (var dependencyXml in DependencyXmlDocs())
                options.IncludeXmlComments(dependencyXml);

            options.OperationFilter<ApiVersionParameterFilter>();
        });

        return services;
    }

    /// <summary>Application-layer DTOs carry their own XML docs; pull them into the schema too.</summary>
    private static IEnumerable<string> DependencyXmlDocs()
    {
        foreach (var name in new[] { "Catalog.Application", "Catalog.Domain" })
        {
            var path = Path.Combine(AppContext.BaseDirectory, $"{name}.xml");
            if (File.Exists(path)) yield return path;
        }
    }
}
