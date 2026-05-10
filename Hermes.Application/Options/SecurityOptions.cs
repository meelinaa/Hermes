namespace Hermes.Application.Options;

/// <summary>Cross-cutting security behaviour for the application layer.</summary>
public sealed class SecurityOptions
{
    public const string SECTION_NAME = "Security";

    /// <summary>When <c>true</c>, e-mail verification challenges are stored as <see cref="Hermes.Application.Security.RefreshTokenHasher"/> output (SHA-256 hex), not the raw six-digit code.</summary>
    public bool HashEmailVerificationCodes { get; set; } = true;
}
