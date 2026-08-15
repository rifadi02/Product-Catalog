Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Destructure.With<RedactingDestructuringPolicy>());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure();

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddAppAuthorization();
    builder.Services.AddAppRateLimiting();

    builder.Services
        .AddControllers(options => options.Filters.Add<FluentValidationFilter>())
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
            options.JsonSerializerOptions.Converters.Add(new TrimmingStringConverter());
        });

    builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .ToDictionary(
                    kv => JsonName(kv.Key),
                    kv => kv.Value!.Errors
                        .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                            ? "The value provided is not valid."
                            : e.ErrorMessage)
                        .ToArray());

            var malformed = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Any(e => e.Exception is System.Text.Json.JsonException);

            var status = StatusCodes.Status400BadRequest;

            var problem = malformed
                ? ProblemFactory.Create(context.HttpContext, status, "malformed_json",
                    "The request body is not valid JSON.")
                : ProblemFactory.Create(context.HttpContext, status, "validation_failed",
                    "One or more validation errors occurred.", errors);

            return new ObjectResult(problem)
            {
                StatusCode = status,
                ContentTypes = { "application/problem+json" }
            };
        };
    });

    builder.Services
        .AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

    builder.Services.AddAppSwagger();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                         ?? [];

    builder.Services.AddCors(o => o.AddPolicy("spa", p =>
    {
        if (allowedOrigins.Length == 0)
        {
            p.WithOrigins("http://localhost:5173", "http://localhost:3000");
        }
        else
        {
            p.WithOrigins(allowedOrigins);
        }

        p.WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
         .WithHeaders("Authorization", "Content-Type", "If-Match", "If-None-Match",
                      CorrelationIdMiddleware.HeaderName)
         .WithExposedHeaders("ETag", "Location", "Retry-After", CorrelationIdMiddleware.HeaderName)
         .AllowCredentials();
    }));

    // Both checks take a factory rather than a string: readiness must probe the database the
    // application actually connected to, which is only known once the container is built.
    // Resolving it here would pin the check to whatever appsettings.json said and let a green
    // /health/ready vouch for an entirely different server.
    var healthChecks = builder.Services.AddHealthChecks();

    healthChecks.AddNpgSql(
        sp => sp.GetCatalogConnectionString(), name: "postgres", tags: ["ready"]);

    healthChecks.AddCheck<CacheHealthCheck>(
        "cache", failureStatus: HealthStatus.Degraded);

    var app = builder.Build();

    app.UseExceptionHandling();
    app.UseCorrelationId();

    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
            diagnosticContext.Set("UserId",
                httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value);
        };
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Product Catalog API v1");
        options.DocumentTitle = "Product Catalog API";
    });

    app.UseCors("spa");

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).AllowAnonymous();

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }).AllowAnonymous();

    await app.ApplyMigrationsAsync();
    await app.SeedDatabaseAsync();

    app.Services.GetRequiredService<CacheModeReport>().Log(app.Logger);

    await app.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

static string JsonName(string modelStateKey)
{
    if (string.IsNullOrEmpty(modelStateKey)) return modelStateKey;

    var leaf = modelStateKey[(modelStateKey.LastIndexOf('.') + 1)..];

    return char.IsLower(leaf[0]) ? leaf : char.ToLowerInvariant(leaf[0]) + leaf[1..];
}

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> in the integration tests has a type to
/// hang off. Top-level statements generate an internal Program class, which the test project
/// cannot see without this.
/// </summary>
public partial class Program;
