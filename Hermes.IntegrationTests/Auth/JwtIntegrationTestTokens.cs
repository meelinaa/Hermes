using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace Hermes.IntegrationTests.Auth;

/// <summary>
/// Builds JWT access tokens that exercise bearer authentication without calling login—mirrors claim shapes from <see cref="Hermes.Application.Security.JwtTokenIssuer"/>.
/// </summary>
internal static class JwtIntegrationTestTokens
{
    /// <summary>Creates a syntactically invalid bearer secret (not three Base64Url segments).</summary>
    public const string MalformedJwtMaterial = "not.a.valid.jwt.structure";

    private static string BuildToken(
        int userId,
        string issuer,
        string audience,
        SymmetricSecurityKey signingKey,
        DateTime notBeforeUtc,
        DateTime expiresUtc)
    {
        string id = userId.ToString(CultureInfo.InvariantCulture);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id),
            new(ClaimTypes.NameIdentifier, id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        };

        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            notBeforeUtc,
            expiresUtc,
            creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>JWT whose <c>exp</c> lies firmly in the past—middleware must reject it even though the signature matches.</summary>
    public static string CreateExpiredAccessToken(int userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IntegrationTestAuthSettings.JwtSigningKey));
        DateTime now = DateTime.UtcNow;
        return BuildToken(
            userId,
            IntegrationTestAuthSettings.JwtIssuer,
            IntegrationTestAuthSettings.JwtAudience,
            key,
            notBeforeUtc: now.AddHours(-2),
            expiresUtc: now.AddMinutes(-45));
    }

    /// <summary>JWT signed with a different symmetric key—signature verification must fail.</summary>
    public static string CreateTokenWithWrongSigningKey(int userId)
    {
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('z', 48)));
        DateTime now = DateTime.UtcNow;
        return BuildToken(
            userId,
            IntegrationTestAuthSettings.JwtIssuer,
            IntegrationTestAuthSettings.JwtAudience,
            wrongKey,
            notBeforeUtc: now.AddMinutes(-5),
            expiresUtc: now.AddMinutes(60));
    }

    /// <summary>JWT whose <c>aud</c> claim does not match configured <see cref="IntegrationTestAuthSettings.JwtAudience"/>.</summary>
    public static string CreateTokenWithWrongAudience(int userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IntegrationTestAuthSettings.JwtSigningKey));
        DateTime now = DateTime.UtcNow;
        return BuildToken(
            userId,
            IntegrationTestAuthSettings.JwtIssuer,
            audience: "wrong-audience.integration.tests",
            key,
            notBeforeUtc: now.AddMinutes(-5),
            expiresUtc: now.AddMinutes(60));
    }

    /// <summary>JWT whose <c>iss</c> claim does not match configured <see cref="IntegrationTestAuthSettings.JwtIssuer"/>.</summary>
    public static string CreateTokenWithWrongIssuer(int userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(IntegrationTestAuthSettings.JwtSigningKey));
        DateTime now = DateTime.UtcNow;
        return BuildToken(
            userId,
            issuer: "wrong-issuer.integration.tests",
            IntegrationTestAuthSettings.JwtAudience,
            key,
            notBeforeUtc: now.AddMinutes(-5),
            expiresUtc: now.AddMinutes(60));
    }
}
