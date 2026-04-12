namespace Hermes.Application.Options;

/// <summary>
/// Configuration for JWT access tokens (signed in the application, validated in the API with the same values).
/// Bound from the <c>Jwt</c> section in appsettings or environment variables.
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Configuration section name (e.g. <c>Jwt</c> in appsettings.json).</summary>
    public const string SectionName = "Jwt";

    /// <summary>Value for the JWT <c>iss</c> claim and for <see cref="Microsoft.IdentityModel.Tokens.TokenValidationParameters.ValidIssuer"/>.</summary>
    public string Issuer { get; set; } = "";

    /// <summary>Value for the JWT <c>aud</c> claim and for token audience validation.</summary>
    public string Audience { get; set; } = "";

    /// <summary>
    /// Shared secret used to sign (HMAC-SHA256) and verify access tokens. Must stay private; use a long random value in production.
    /// </summary>
    public string SigningKey { get; set; } = "";

    /// <summary>Lifetime of JWT access tokens in minutes (short, since refresh tokens extend the session).</summary>
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>How long opaque refresh tokens remain valid if not revoked (stored hashed in the database).</summary>
    public int RefreshTokenDays { get; set; } = 14;
}
