using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hermes.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hermes.Application.Security;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : IJwtTokenIssuer
{
    public JwtAccessTokenResult Issue(int userId, string? email, string? name)
    {
        var o = options.Value;
        var id = userId.ToString(CultureInfo.InvariantCulture);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id),
            new(ClaimTypes.NameIdentifier, id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        };

        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(ClaimTypes.Email, email.Trim()));
        if (!string.IsNullOrWhiteSpace(name))
            claims.Add(new Claim(ClaimTypes.Name, name.Trim()));

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
