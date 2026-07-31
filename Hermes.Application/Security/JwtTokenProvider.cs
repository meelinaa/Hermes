using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hermes.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hermes.Application.Security;

public sealed class JwtTokenProvider(IOptions<JwtOptions> options) : IJwtTokenProvider
{
    public JwtAccessTokenResultDto Issue(int userId, string? email, string? name)
    {
        if(userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        JwtOptions jwtOptions = options.Value;
        string? id = userId.ToString(CultureInfo.InvariantCulture);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, id),
            new(ClaimTypes.NameIdentifier, id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        ];

        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(ClaimTypes.Email, email.Trim()));
        if (!string.IsNullOrWhiteSpace(name))
            claims.Add(new Claim(ClaimTypes.Name, name.Trim()));

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
        return new JwtAccessTokenResultDto(jwt, new DateTimeOffset(expires, TimeSpan.Zero));
    }
}
