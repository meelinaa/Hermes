namespace Hermes.Notifications.Receiving.Models;

/// <summary>
/// A message retrieved from a mailbox or test server.
/// </summary>
/// <param name="Id">Server-specific message identifier.</param>
/// <param name="From">Formatted sender.</param>
/// <param name="To">Formatted recipient list.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="Body">Message body text.</param>
/// <param name="ReceivedAt">When the message was received.</param>
public sealed record EmailResult(
    string Id,
    string From,
    string To,
    string Subject,
    string Body,
    DateTimeOffset ReceivedAt);
