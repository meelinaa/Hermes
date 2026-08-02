using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hermes.Application.DTOs.Security;
using Hermes.Application.Options;
using Hermes.Application.Services.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Hermes.UnitTests.Security;

public sealed class JwtTokenIssuerTests
{
    private static JwtOptions CreateValidOptions() =>
        new()
        {
            Issuer = "https://hermes.tests/",
            Audience = "hermes-api-tests",
            SigningKey = new string('k', 48),
            AccessTokenMinutes = 120,
        };

    /// <summary>Clears default inbound claim-type map so <c>sub</c> round-trips consistently under validation.</summary>
    private static TokenValidationParameters CreateValidation(JwtOptions o, JwtSecurityTokenHandler handler)
    {
        TokenValidationParameters p = new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey)),
            ValidIssuer = o.Issuer,
            ValidAudience = o.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        };
        handler.InboundClaimTypeMap.Clear();
        return p;
    }

    [Fact]
    public void Issue_Should_Embed_SubClaim_WithUserIdString_AndAllowValidation()
    {
        JwtOptions o = CreateValidOptions();
        JwtTokenProvider issuer = new(Options.Create(o));

        JwtAccessTokenResultDto result = issuer.Issue(42, "user@site.test", "  Name  ");

        JwtSecurityTokenHandler handler = new();
        ClaimsPrincipal principal = handler.ValidateToken(result.Token, CreateValidation(o, handler), out SecurityToken validatedToken);
        JwtSecurityToken jwt = Assert.IsType<JwtSecurityToken>(validatedToken);

        Assert.Equal(o.Issuer, jwt.Issuer);
        Assert.Equal(o.Audience, jwt.Audiences.Single());
        Assert.Equal("42", jwt.Payload[JwtRegisteredClaimNames.Sub]?.ToString());
        Assert.Equal("42", principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("user@site.test", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Name", principal.FindFirstValue(ClaimTypes.Name));
        Assert.False(string.IsNullOrEmpty(principal.FindFirstValue(JwtRegisteredClaimNames.Jti)));
        Assert.False(string.IsNullOrEmpty(principal.FindFirstValue(JwtRegisteredClaimNames.Iat)));
    }

    [Fact]
    public void Issue_Should_OmitOptionalClaims_WhenEmailAndNameMissingOrWhitespace()
    {
        JwtOptions o = CreateValidOptions();
        JwtTokenProvider issuer = new(Options.Create(o));

        JwtAccessTokenResultDto result = issuer.Issue(1, null, "   ");

        JwtSecurityTokenHandler handler = new();
        ClaimsPrincipal principal = handler.ValidateToken(result.Token, CreateValidation(o, handler), out _);

        Assert.Null(principal.FindFirstValue(ClaimTypes.Email));
        Assert.Null(principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("1", principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
    }

    [Fact]
    public void Issue_Should_GenerateDistinctCompactTokens_PerIssuance()
    {
        JwtOptions o = CreateValidOptions();
        JwtTokenProvider issuer = new(Options.Create(o));

        JwtAccessTokenResultDto a = issuer.Issue(1, "a@test", "A");
        JwtAccessTokenResultDto b = issuer.Issue(1, "a@test", "A");

        Assert.NotEqual(a.Token, b.Token);
    }

    [Fact]
    public void Issue_Should_SetExpiryWithinConfiguredAccessMinutes()
    {
        JwtOptions o = CreateValidOptions();
        o.AccessTokenMinutes = 5;
        JwtTokenProvider issuer = new(Options.Create(o));
        DateTime before = DateTime.UtcNow;

        JwtAccessTokenResultDto result = issuer.Issue(1, null, null);

        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(result.Token);
        DateTime exp = jwt.ValidTo;
        Assert.InRange(exp, before.AddMinutes(4.5), before.AddMinutes(5.5));
        Assert.True(Math.Abs((result.ExpiresAtUtc.UtcDateTime - exp).TotalSeconds) < 2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Issue_Should_RejectNonPositiveUserIdentifier(int invalidUserId)
    {
        JwtTokenProvider issuer = new(Options.Create(CreateValidOptions()));

        Assert.Throws<ArgumentOutOfRangeException>(() => issuer.Issue(invalidUserId, null, null));
    }
}
