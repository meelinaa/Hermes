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
        if(userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        JwtOptions jwtOptions = options.Value;
        string? id = userId.ToString(CultureInfo.InvariantCulture);

        // Claims become part of the signed payload; clients can read them (JWT is only signed, not encrypted).
        List<Claim> claims =
        [
            // Standard subject: who the token is about (we store the numeric user id as string).
            new(JwtRegisteredClaimNames.Sub, id),
            // ASP.NET maps this to NameIdentifier for User.Identity.
            new(ClaimTypes.NameIdentifier, id),
            // New unique id per token issuance — helps distinguish tokens and supports revocation patterns on the client.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            // Issued-at time (Unix seconds).
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        ];

        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(ClaimTypes.Email, email.Trim()));
        if (!string.IsNullOrWhiteSpace(name))
            claims.Add(new Claim(ClaimTypes.Name, name.Trim()));

        // Same key bytes the API uses in JwtBearer TokenValidationParameters.
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);
        DateTime expires = DateTime.UtcNow.AddMinutes(jwtOptions.AccessTokenMinutes);

        JwtSecurityToken token = new(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);

        string? jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new JwtAccessTokenResult(jwt, new DateTimeOffset(expires, TimeSpan.Zero));
    }
}
