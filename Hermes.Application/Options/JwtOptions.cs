namespace Hermes.Application.Options;

/// <summary>Symmetric JWT settings (issuer signs access tokens; API validates the same key).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>JWT <c>iss</c> claim and validation.</summary>
    public string Issuer { get; set; } = "";

    /// <summary>JWT <c>aud</c> claim and validation.</summary>
    public string Audience { get; set; } = "";

    /// <summary>HS256 signing key (UTF-8); use at least 32 bytes (256 bits) of entropy in production.</summary>
    public string SigningKey { get; set; } = "";

    /// <summary>Lifetime of access tokens.</summary>
    public int AccessTokenMinutes { get; set; } = 1440;
}
