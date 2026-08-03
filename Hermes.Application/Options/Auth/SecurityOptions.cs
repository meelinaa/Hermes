namespace Hermes.Application.Options.Auth;

/// <summary>
/// Configuration options for application security and verification challenge hashing behavior.
/// </summary>
public sealed class SecurityOptions
{
    public const string SECTION_NAME = "Security";

    /// <summary>false = store six-digit code as plaintext (legacy installs only).</summary>
    public bool HashEmailVerificationCodes { get; set; } = true;
}
