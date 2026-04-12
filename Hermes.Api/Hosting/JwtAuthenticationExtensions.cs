using System.Security.Claims;
using System.Text;
using Hermes.Application.Options;
using Hermes.Application.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
        // Values merge from appsettings*.json, then environment (e.g. Jwt__SigningKey). Do not commit production secrets.
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);

        var jwt = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException($"Missing configuration section '{JwtOptions.SectionName}'.");

        // HS256 needs enough key material; require at least 32 characters (set Jwt:SigningKey in Development or Jwt__SigningKey in Production).
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

        // Stateless signing service: same key as TokenValidationParameters below.
        services.AddSingleton<IJwtTokenIssuer, JwtTokenIssuer>();

        // Default scheme = Bearer; middleware will parse JWT, validate signature/lifetime/iss/aud, and populate HttpContext.User.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Signature must match our SigningKey; tampered tokens fail here.
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    // Must match the "iss" claim we put in the token when issuing.
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    // Must match the "aud" claim.
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    // Reject expired tokens (exp claim).
                    ValidateLifetime = true,
                    // Small clock tolerance so minor skew between servers does not reject valid tokens.
                    ClockSkew = TimeSpan.FromMinutes(1),
                    // Maps the name identifier claim so User.FindFirstValue(ClaimTypes.NameIdentifier) returns the user id.
                    NameClaimType = ClaimTypes.NameIdentifier,
                };
            });

        services.AddAuthorization();
        return services;
    }
}
