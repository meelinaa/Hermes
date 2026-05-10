namespace Hermes.Application.Options;

public sealed class SecurityOptions
{
    public const string SECTION_NAME = "Security";

    /// <summary>false = store six-digit code as plaintext (legacy installs only).</summary>
    public bool HashEmailVerificationCodes { get; set; } = true;
}
