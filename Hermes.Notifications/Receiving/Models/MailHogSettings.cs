namespace Hermes.Notifications.Receiving.Models;

/// <summary>
/// Base URL for the MailHog HTTP API (e.g. <c>http://localhost:8025</c>).
/// Uses a parameterless constructor so <c>IOptions&lt;MailHogSettings&gt;</c> / configuration binding works.
/// </summary>
public sealed class MailHogSettings
{
    /// <summary>Root URL without a trailing path segment for the API.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c>, the worker sends a short test e-mail over SMTP after each minutely newsletter scheduler run (for MailHog).
    /// </summary>
    public bool SendSchedulerTestMailEachMinute { get; set; }
}
