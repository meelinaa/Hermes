using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hermes.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hermes.Application.Security;

/// <summary>
/// Builds short-lived JWT access tokens signed with HMAC-SHA256 (symmetric key from <see cref="JwtOptions"/>).
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : IJwtTokenIssuer
{
    /// <inheritdoc />
    public JwtAccessTokenResult Issue(int userId, string? email, string? name)
    {
        var o = options.Value;
        var id = userId.ToString(CultureInfo.InvariantCulture);

        // Claims become part of the signed payload; clients can read them (JWT is only signed, not encrypted).
        var claims = new List<Claim>
        {
            // Standard subject: who the token is about (we store the numeric user id as string).
            new(JwtRegisteredClaimNames.Sub, id),
            // ASP.NET maps this to NameIdentifier for User.Identity.
            new(ClaimTypes.NameIdentifier, id),
            // New unique id per token issuance — helps distinguish tokens and supports revocation patterns on the client.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            // Issued-at time (Unix seconds).
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        };

        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(ClaimTypes.Email, email.Trim()));
        if (!string.IsNullOrWhiteSpace(name))
            claims.Add(new Claim(ClaimTypes.Name, name.Trim()));

        // Same key bytes the API uses in JwtBearer TokenValidationParameters.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(o.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: o.Issuer,
            audience: o.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new JwtAccessTokenResult(jwt, new DateTimeOffset(expires, TimeSpan.Zero));
    }
}
