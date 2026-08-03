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

public static class JwtAuthenticationExtensions
{
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
