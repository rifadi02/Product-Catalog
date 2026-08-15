// Scoped to this file rather than GlobalUsings.cs: the API already globally imports
// Catalog.Application.Common, which makes the bare name `Options` resolve to the
// Catalog.Application.Common.Options *namespace*. Importing Microsoft.Extensions.Options globally
// would put a type of the same name in scope everywhere and make that name ambiguous.
using Microsoft.Extensions.Options;

namespace Catalog.Api.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Registers JWT bearer authentication.
    ///
    /// <para>The signing key, issuer and audience are read through <see cref="IOptions{TOptions}"/>
    /// when the bearer handler's options are first materialised — never here. Reading them at
    /// registration time is what silently splits the two halves of authentication apart: token
    /// *issuing* goes through <c>IOptions&lt;JwtOptions&gt;</c> and so sees the final
    /// configuration, while token *validation* would be frozen against whatever was loaded before
    /// the host finished composing. The result is an API that hands out tokens it then rejects
    /// with 401. See the note on <c>Catalog.Infrastructure.DependencyInjection</c>.</para>
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // MinLength on the property counts characters; HS256 cares about bytes. For a
            // non-ASCII key the two disagree, and only this one is the real constraint.
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
                "Jwt:SigningKey must be at least 32 bytes for HS256.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.ConfigureOptions<ConfigureJwtBearerOptions>();

        return services;
    }
}

/// <summary>
/// Builds the bearer handler's <see cref="TokenValidationParameters"/> from the resolved
/// <see cref="JwtOptions"/>, so validation and issuing are guaranteed to agree.
/// </summary>
internal sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    /// <summary>The scheme is always resolved by name, so the unnamed instance stays untouched.</summary>
    public void Configure(JwtBearerOptions options) { }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme) return;

        var jwt = jwtOptions.Value;

        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                                   Encoding.UTF8.GetBytes(jwt.SigningKey)),

            ClockSkew = TimeSpan.Zero,

            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                if (ctx.Exception is SecurityTokenExpiredException)
                    ctx.Response.Headers.Append("x-token-expired", "true");
                return Task.CompletedTask;
            },

            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();

                var expired = ctx.Response.Headers.ContainsKey("x-token-expired");
                var hasHeader = ctx.Request.Headers.ContainsKey("Authorization");

                var code = expired ? "token_expired"
                         : !hasHeader ? "missing_token"
                         : "invalid_token";

                await ctx.Response.WriteProblemAsync(
                    StatusCodes.Status401Unauthorized,
                    code,
                    "Authentication is required to access this resource.");
            },

            OnForbidden = ctx => ctx.Response.WriteProblemAsync(
                StatusCodes.Status403Forbidden,
                "insufficient_role",
                "You do not have permission to perform this action.")
        };
    }
}
