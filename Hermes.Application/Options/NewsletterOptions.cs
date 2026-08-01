namespace Hermes.Application.Options;

public sealed class NewsletterOptions
{
    public const string SECTION_NAME = "Newsletter";

    public string? SchedulingNote { get; set; }

    public string TimeZoneId { get; set; } = string.Empty;
}
