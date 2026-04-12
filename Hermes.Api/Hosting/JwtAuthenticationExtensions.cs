using System.Security.Claims;
using System.Text;
using Hermes.Application.Options;
using Hermes.Application.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Hermes.Api.Hosting;

public static class JwtAuthenticationExtensions
{
    /// <summary>Registers JWT bearer validation, authorization, and <see cref="IJwtTokenIssuer"/> for login.</summary>
    public static IServiceCollection AddHermesJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);

        var jwt = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException($"Missing configuration section '{JwtOptions.SectionName}'.");

        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:SigningKey must be at least 32 characters (256-bit entropy for HS256).");
        }

        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
        {
            throw new InvalidOperationException(
                $"{JwtOptions.SectionName}:Issuer and Audience must be set.");
        }

        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();

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

        services.AddAuthorization();
        return services;
    }
}
