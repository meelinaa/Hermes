using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hermes.Application.Options;
using Hermes.Application.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Hermes.UnitTests.Security;

/// <summary>
/// Specifications for access JWT issuance: token must validate under RFC-style checks; issuer rejects invalid IDs and keys.
/// </summary>
/// <remarks>
/// <see cref="JwtSecurityTokenHandler.ValidateToken"/> applies inbound claim-type mapping by default. Tests clear
/// <see cref="JwtSecurityTokenHandler.InboundClaimTypeMap"/> so standard claims like <c>sub</c> round-trip consistently.
/// For raw payload claims you can also read <see cref="JwtSecurityToken.Payload"/>.
/// </remarks>
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

    /// <summary>
    /// Builds validation parameters matching issuer settings and clears inbound claim mapping for predictable <c>sub</c> reads.
    /// </summary>
    private static TokenValidationParameters CreateValidation(JwtOptions o, JwtSecurityTokenHandler handler)
    {
        var p = new TokenValidationParameters
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

    /// <summary>
    /// Ensures issued tokens embed <c>sub</c> as the user id string, validate cryptographically, and expose expected claims after trimming optional fields.
    /// </summary>
    [Fact]
    public void Issue_Should_Embed_SubClaim_WithUserIdString_AndAllowValidation()
    {
        // Arrange
        JwtOptions o = CreateValidOptions();
        JwtTokenIssuer issuer = new(Options.Create(o));

        // Act
        JwtAccessTokenResult result = issuer.Issue(42, "user@site.test", "  Name  ");

        // Assert — validate signature/issuer/audience and inspect claims
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

    /// <summary>
    /// Optional email/name claims must be omitted when missing or whitespace-only so the token stays minimal.
    /// </summary>
    [Fact]
    public void Issue_Should_OmitOptionalClaims_WhenEmailAndNameMissingOrWhitespace()
    {
        // Arrange
        JwtOptions o = CreateValidOptions();
        JwtTokenIssuer issuer = new(Options.Create(o));

        // Act
        JwtAccessTokenResult result = issuer.Issue(1, null, "   ");

        // Assert
        JwtSecurityTokenHandler handler = new();
        ClaimsPrincipal principal = handler.ValidateToken(result.Token, CreateValidation(o, handler), out _);

        Assert.Null(principal.FindFirstValue(ClaimTypes.Email));
        Assert.Null(principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("1", principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
    }

    /// <summary>
    /// Each issuance must produce a distinct compact token (jti/timing uniqueness), even when inputs match.
    /// </summary>
    [Fact]
    public void Issue_Should_GenerateDistinctCompactTokens_PerIssuance()
    {
        // Arrange
        JwtOptions o = CreateValidOptions();
        JwtTokenIssuer issuer = new(Options.Create(o));

        // Act
        JwtAccessTokenResult a = issuer.Issue(1, "a@test", "A");
        JwtAccessTokenResult b = issuer.Issue(1, "a@test", "A");

        // Assert
        Assert.NotEqual(a.Token, b.Token);
    }

    /// <summary>
    /// Expiry (<c>exp</c>) should align with configured access-token lifetime (within clock skew tolerance).
    /// </summary>
    [Fact]
    public void Issue_Should_SetExpiryWithinConfiguredAccessMinutes()
    {
        // Arrange
        JwtOptions o = CreateValidOptions();
        o.AccessTokenMinutes = 5;
        JwtTokenIssuer issuer = new(Options.Create(o));
        DateTime before = DateTime.UtcNow;

        // Act
        JwtAccessTokenResult result = issuer.Issue(1, null, null);

        // Assert
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(result.Token);
        DateTime exp = jwt.ValidTo;
        Assert.InRange(exp, before.AddMinutes(4.5), before.AddMinutes(5.5));
        Assert.True(Math.Abs((result.ExpiresAtUtc.UtcDateTime - exp).TotalSeconds) < 2);
    }

    /// <summary>
    /// Negative contract: user ids must be positive integers before issuing a token.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Issue_Should_RejectNonPositiveUserIdentifier(int invalidUserId)
    {
        // Arrange
        JwtTokenIssuer issuer = new(Options.Create(CreateValidOptions()));

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => issuer.Issue(invalidUserId, null, null));
    }
}
