namespace Catalog.Api.Extensions;

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Register = "register";
}

public static class RateLimitingExtensions
{
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            o.AddPolicy(RateLimitPolicies.Login, ctx => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"login:{ClientKey(ctx)}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5) }));

            o.AddPolicy(RateLimitPolicies.Register, ctx => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"register:{ClientKey(ctx)}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15) }));

            o.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();

                await ctx.HttpContext.Response.WriteProblemAsync(
                    StatusCodes.Status429TooManyRequests,
                    "rate_limited",
                    "Too many requests. Please try again later.");

                _ = ct;
            };
        });

        return services;
    }

    private static string ClientKey(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
