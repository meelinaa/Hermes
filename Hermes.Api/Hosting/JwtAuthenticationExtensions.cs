using System.Security.Claims;
using System.Text;
using Hermes.Api.Authorization;
using Hermes.Application.Options;
using Hermes.Application.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Hermes.Api.Hosting;

/// <summary>
/// Wires ASP.NET Core authentication so incoming requests can carry a JWT in the
/// <c>Authorization: Bearer &lt;token&gt;</c> header. The same symmetric key and issuer/audience as in
/// <see cref="JwtOptions"/> must be used when signing tokens in <see cref="IJwtTokenIssuer"/>.
/// </summary>
public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Binds <see cref="JwtOptions"/> from configuration, registers <see cref="IJwtTokenIssuer"/> for creating tokens at login,
    /// and configures the JWT bearer handler to validate tokens on each request to <c>[Authorize]</c> endpoints.
    /// </summary>
    public static IServiceCollection AddHermesJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection jwtSection = configuration.GetSection(JwtOptions.SECTION_NAME);
        services.Configure<JwtOptions>(jwtSection);

        JwtOptions jwt = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException($"Missing configuration section '{JwtOptions.SECTION_NAME}'.");

        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SECTION_NAME}:SigningKey must be at least 32 characters (256-bit entropy for HS256).");
        }

        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SECTION_NAME}:Issuer and Audience must be set.");
        }

        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();

        services.AddSingleton<IAuthorizationHandler, RouteUserMatchesClaimHandler>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ClaimTypes.NameIdentifier,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new RouteUserMatchesClaimRequirement("userId"));
            });
            options.AddPolicy(HermesAuthorizationPolicies.OWN_USER_ROUTE_ID, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new RouteUserMatchesClaimRequirement("id"));
            });
        });
        return services;
    }
}
