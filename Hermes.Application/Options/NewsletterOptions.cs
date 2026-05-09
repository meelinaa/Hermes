namespace Hermes.Application.Options;

/// <summary>Worker newsletter scheduler settings under configuration section <c>Newsletter</c>.</summary>
public sealed class NewsletterOptions
{
    public const string SectionName = "Newsletter";

    /// <summary>Optional note in config files (ignored by the app; kept for operator documentation).</summary>
    public string? SchedulingNote { get; set; }

    /// <summary>
    /// Time zone for matching send slots (IANA id on Linux/macOS, e.g. <c>Europe/Berlin</c>; Windows accepts IANA on supported OS builds or a Windows id).
    /// Empty or whitespace means <see cref="TimeZoneInfo.Local"/> (set the process/container <c>TZ</c> if you rely on this).
    /// </summary>
    public string TimeZoneId { get; set; } = string.Empty;
}
