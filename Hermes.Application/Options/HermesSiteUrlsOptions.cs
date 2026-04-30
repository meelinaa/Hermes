namespace Hermes.Application.Options;

/// <summary>
/// Public site URLs and support contact used in transactional e-mails (unsubscribe, settings, support).
/// </summary>
public sealed class HermesSiteUrlsOptions
{
    public const string SectionName = "Hermes";

    /// <summary>Scheme + host for deep links, e.g. <c>https://hermes.de</c> (no trailing slash).</summary>
    public string PublicBaseUrl { get; set; } = "https://hermes.de";

    /// <summary>Support inbox shown in verification e-mails (address only, no <c>mailto:</c> prefix).</summary>
    public string SupportEmail { get; set; } = "support@hermes.de";
}
