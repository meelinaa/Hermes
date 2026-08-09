using System.ComponentModel.DataAnnotations;

namespace Hermes.Notifications.Receiving.Options;

/// <summary>
/// Configuration options for connecting to the MailHog HTTP API and controlling dev mail features.
/// </summary>
public sealed class MailHogOptions
{
    /// <summary>
    /// Gets or sets the base URL of the MailHog Web UI / API endpoint (e.g. http://localhost:8025).
    /// </summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether a diagnostic test mail is sent to MailHog on every scheduler run.
    /// </summary>
    public bool SendSchedulerTestMailEachMinute { get; set; }
}
