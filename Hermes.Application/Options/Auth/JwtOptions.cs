using System.ComponentModel.DataAnnotations;

namespace Hermes.Application.Options.Auth;

/// <summary>
/// Configuration options for JSON Web Token (JWT) issuance and validation.
/// </summary>
public sealed class JwtOptions
{
    public const string SECTION_NAME = "Jwt";

    [Required]
    public string Issuer { get; set; } = null!;

    [Required]
    public string Audience { get; set; } = null!;

    /// <summary>Symmetric HS256 secret — use a long random value outside source control in production.</summary>
    [Required]
    public string SigningKey { get; set; } = null!;

    [Range(1, 10080)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>
    /// Absolute maximum session lifetime in days from initial credential authentication.
    /// Prevents indefinite sliding token renewal.
    /// </summary>
    [Range(1, 365)]
    public int MaxSessionDays { get; set; } = 30;
}
