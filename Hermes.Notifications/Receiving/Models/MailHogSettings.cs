namespace Hermes.Notifications.Receiving.Models;

/// <summary>
/// Base URL for the MailHog HTTP API (e.g. <c>http://localhost:8025</c>).
/// </summary>
/// <param name="BaseUrl">Root URL without a trailing path segment for the API.</param>
public sealed record MailHogSettings(string BaseUrl);
