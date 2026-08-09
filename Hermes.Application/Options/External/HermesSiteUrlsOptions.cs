using System.ComponentModel.DataAnnotations;

namespace Hermes.Application.Options.External;

/// <summary>
/// Configuration options for public site URLs and support contact endpoints used in email template links.
/// </summary>
public sealed class HermesSiteUrlsOptions
{
    public const string SECTION_NAME = "Hermes";

    [Required]
    [Url]
    public string PublicBaseUrl { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string SupportEmail { get; set; } = null!;
}
