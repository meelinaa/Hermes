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
    public const string MALFORMED_JWT_MATERIAL = "not.a.valid.jwt.structure";

    private static string BuildToken(
        int userId,
        string issuer,
        string audience,
        SymmetricSecurityKey signingKey,
        DateTime notBeforeUtc,
        DateTime expiresUtc)
    {
        string id = userId.ToString(CultureInfo.InvariantCulture);
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, id),
            new(ClaimTypes.NameIdentifier, id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        ];

        SigningCredentials creds = new(signingKey, SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
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
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(IntegrationTestAuthSettings.JWT_SIGNING_KEY));
        DateTime now = DateTime.UtcNow;
        return BuildToken(
            userId,
            IntegrationTestAuthSettings.JWT_ISSUER,
            IntegrationTestAuthSettings.JWT_AUDIENCE,
            key,
            notBeforeUtc: now.AddHours(-2),
            expiresUtc: now.AddMinutes(-45));
    }

    /// <summary>JWT signed with a different symmetric key—signature verification must fail.</summary>
    public static string CreateTokenWithWrongSigningKey(int userId)
    {
        SymmetricSecurityKey wrongKey = new(Encoding.UTF8.GetBytes(new string('z', 48)));
        DateTime now = DateTime.UtcNow;
        return BuildToken(
            userId,
            IntegrationTestAuthSettings.JWT_ISSUER,
            IntegrationTestAuthSettings.JWT_AUDIENCE,
            wrongKey,
            notBeforeUtc: now.AddMinutes(-5),
            expiresUtc: now.AddMinutes(60));
    }

    /// <summary>JWT whose <c>aud</c> claim does not match configured <see cref="IntegrationTestAuthSettings.JWT_AUDIENCE"/>.</summary>
    public static string CreateTokenWithWrongAudience(int userId)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(IntegrationTestAuthSettings.JWT_SIGNING_KEY));
        DateTime now = DateTime.UtcNow;
        return BuildToken(
            userId,
            IntegrationTestAuthSettings.JWT_ISSUER,
            audience: "wrong-audience.integration.tests",
            key,
            notBeforeUtc: now.AddMinutes(-5),
            expiresUtc: now.AddMinutes(60));
    }

    /// <summary>JWT whose <c>iss</c> claim does not match configured <see cref="IntegrationTestAuthSettings.JWT_ISSUER"/>.</summary>
    public static string CreateTokenWithWrongIssuer(int userId)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(IntegrationTestAuthSettings.JWT_SIGNING_KEY));
        DateTime now = DateTime.UtcNow;
        return BuildToken(
            userId,
            issuer: "wrong-issuer.integration.tests",
            IntegrationTestAuthSettings.JWT_AUDIENCE,
            key,
            notBeforeUtc: now.AddMinutes(-5),
            expiresUtc: now.AddMinutes(60));
    }
}
