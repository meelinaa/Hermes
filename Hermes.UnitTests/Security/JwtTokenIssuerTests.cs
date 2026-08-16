using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hermes.Application.DTOs.Security;
using Hermes.Application.Options.Auth;
using Hermes.Application.Services.Security;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Hermes.UnitTests.Security;

/// <summary>
/// Contains unit tests for <see cref="JwtTokenProvider"/>, validating symmetric key signing,
/// standard JWT claims generation, and token expiration.
/// </summary>
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
    /// Clears default inbound claim-type map so claims round-trip consistently under validation.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="JwtTokenProvider.Issue"/> embeds subject, email, and name claims into the signed token.
    /// </summary>
    [Fact]
    public void Issue_Should_Embed_SubClaim_WithUserIdString_AndAllowValidation()
    {
        // Arrange
        JwtOptions o = CreateValidOptions();
        JwtTokenProvider issuer = new(Options.Create(o), TimeProvider.System);

        // Act
        JwtAccessTokenResultDto result = issuer.Issue(new UserId(42), "user@site.test", "  Name  ");

        // Assert
        JwtSecurityTokenHandler handler = new();
        ClaimsPrincipal principal = handler.ValidateToken(result.Token, CreateValidation(o, handler), out SecurityToken validatedToken);
        JwtSecurityToken jwt = Assert.IsType<JwtSecurityToken>(validatedToken);

        Assert.Equal(o.Issuer, jwt.Issuer);
        Assert.Equal(o.Audience, jwt.Audiences.Single());
        Assert.Equal("42", jwt.Payload[JwtRegisteredClaimNames.Sub]?.ToString());
        Assert.Equal("42", principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
        Assert.Equal("user@site.test", principal.FindFirstValue(JwtRegisteredClaimNames.Email));
        Assert.Equal("Name", principal.FindFirstValue(JwtRegisteredClaimNames.Name));
        Assert.False(string.IsNullOrEmpty(principal.FindFirstValue(JwtRegisteredClaimNames.Jti)));
        Assert.False(string.IsNullOrEmpty(principal.FindFirstValue(JwtRegisteredClaimNames.Iat)));
    }

    /// <summary>
    /// Tests that optional claims like email and display name are omitted when null or whitespace.
    /// </summary>
    [Fact]
    public void Issue_Should_OmitOptionalClaims_WhenEmailAndNameMissingOrWhitespace()
    {
        // Arrange
        JwtOptions o = CreateValidOptions();
        JwtTokenProvider issuer = new(Options.Create(o), TimeProvider.System);

        // Act
        JwtAccessTokenResultDto result = issuer.Issue(new UserId(1), null, "   ");

        // Assert
        JwtSecurityTokenHandler handler = new();
        ClaimsPrincipal principal = handler.ValidateToken(result.Token, CreateValidation(o, handler), out _);

        Assert.Null(principal.FindFirstValue(JwtRegisteredClaimNames.Email));
        Assert.Null(principal.FindFirstValue(JwtRegisteredClaimNames.Name));
        Assert.Equal("1", principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
    }

    /// <summary>
    /// Tests that multiple issuance calls produce unique JWT strings with distinct JTI identifiers.
    /// </summary>
    [Fact]
    public void Issue_Should_GenerateDistinctCompactTokens_PerIssuance()
    {
        // Arrange
        JwtOptions o = CreateValidOptions();
        JwtTokenProvider issuer = new(Options.Create(o), TimeProvider.System);

        // Act
        JwtAccessTokenResultDto a = issuer.Issue(new UserId(1), "a@test", "A");
        JwtAccessTokenResultDto b = issuer.Issue(new UserId(1), "a@test", "A");

        // Assert
        Assert.NotEqual(a.Token, b.Token);
    }

    /// <summary>
    /// Tests that the token expiration date matches the configured access minutes.
    /// </summary>
    [Fact]
    public void Issue_Should_SetExpiryWithinConfiguredAccessMinutes()
    {
        // Arrange
        JwtOptions o = CreateValidOptions();
        o.AccessTokenMinutes = 5;
        JwtTokenProvider issuer = new(Options.Create(o), TimeProvider.System);
        DateTime before = DateTime.UtcNow;

        // Act
        JwtAccessTokenResultDto result = issuer.Issue(new UserId(1), null, null);

        // Assert
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(result.Token);
        DateTime exp = jwt.ValidTo;
        Assert.InRange(exp, before.AddMinutes(4.5), before.AddMinutes(5.5));
        Assert.True(Math.Abs((result.ExpiresAtUtc.UtcDateTime - exp).TotalSeconds) < 2);
    }

    /// <summary>
    /// Tests that passing a non-positive user ID throws an <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Issue_Should_RejectNonPositiveUserIdentifier(int invalidUserId)
    {
        // Arrange
        JwtTokenProvider issuer = new(Options.Create(CreateValidOptions()), TimeProvider.System);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => issuer.Issue(new UserId(invalidUserId), null, null));
    }
}
