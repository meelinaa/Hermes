using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

using Hermes.Api.Authorization;
using Hermes.Api.Constants;
using Hermes.Application.Options.Auth;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Security;

namespace Hermes.Api.Hosting;

/// <summary>
/// Extension methods for configuring JWT bearer authentication schemes and custom authorization policies in DI.
/// </summary>
public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Configures JWT bearer authentication parameters, registers token issuer services, and sets up route authorization policies.
    /// </summary>
    /// <param name="services">The service collection instance.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The updated service collection instance.</returns>
    public static IServiceCollection AddHermesJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection jwtSection = configuration.GetSection(JwtOptions.SECTION_NAME);
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SECTION_NAME).ValidateDataAnnotations().ValidateOnStart();

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

        services.AddSingleton<IJwtTokenProvider, JwtTokenProvider>();

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
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = ClaimTypes.NameIdentifier,
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_USER_ID, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new RouteUserMatchesClaimPolicy("userId"));
            });
            options.AddPolicy(HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_ID, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new RouteUserMatchesClaimPolicy("id"));
            });
        });
        return services;
    }
}
