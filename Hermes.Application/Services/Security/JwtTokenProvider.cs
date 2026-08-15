using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hermes.Application.DTOs.Security;
using Hermes.Application.Options.Auth;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Hermes.Application.Services.Security;

/// <summary>
/// Issues JWT access tokens signed with the configured HMAC-SHA256 key using standard OpenID Connect claims.
/// </summary>
public sealed class JwtTokenProvider(IOptions<JwtOptions> options, TimeProvider timeProvider) : IJwtTokenProvider
{
    /// <summary>
    /// Issues a signed JWT access token for the specified user and returns its expiry time.
    /// </summary>
    /// <param name="userId">The unique user identifier to embed in the token.</param>
    /// <param name="email">Optional email claim.</param>
    /// <param name="name">Optional display name claim.</param>
    /// <returns>A DTO containing the signed token and its UTC expiry.</returns>
    public JwtAccessTokenResultDto Issue(UserId userId, string? email, string? name)
    {
        if (userId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        JwtOptions jwtOptions = options.Value;
        string? id = userId.Value.ToString(CultureInfo.InvariantCulture);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        ];

        if (!string.IsNullOrWhiteSpace(email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email.Trim()));
        if (!string.IsNullOrWhiteSpace(name))
            claims.Add(new Claim(JwtRegisteredClaimNames.Name, name.Trim()));

        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);
        DateTime expires = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(jwtOptions.AccessTokenMinutes);

        JwtSecurityToken token = new(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            notBefore: timeProvider.GetUtcNow().UtcDateTime,
            expires: expires,
            signingCredentials: creds);

        string? jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new JwtAccessTokenResultDto(jwt, new DateTimeOffset(expires, TimeSpan.Zero));
    }
}
