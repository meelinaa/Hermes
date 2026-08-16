using System.ComponentModel.DataAnnotations;

namespace Hermes.Application.Options.Newsletter;

/// <summary>
/// Configuration options for newsletter dispatch scheduling and timezone evaluation.
/// </summary>
public sealed class NewsletterOptions
{
    public const string SECTION_NAME = "Newsletter";

    public string? SchedulingNote { get; set; }

    [Required]
    public string TimeZoneId { get; set; } = null!;
}
