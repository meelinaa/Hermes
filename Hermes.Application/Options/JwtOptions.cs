namespace Hermes.Application.Options;

public sealed class JwtOptions
{
    public const string SECTION_NAME = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>Symmetric HS256 secret — use a long random value outside source control in production.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 14;
}
