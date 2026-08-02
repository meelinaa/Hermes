namespace Hermes.Notifications.Receiving.DTOs;

/// <summary>
/// Data transfer object representing a received email message retrieved from MailHog.
/// </summary>
/// <param name="Id">The unique message identifier.</param>
/// <param name="From">The sender email address.</param>
/// <param name="To">The recipient email address(es).</param>
/// <param name="Subject">The email subject header.</param>
/// <param name="Body">The email content body.</param>
/// <param name="ReceivedAt">The timestamp when the email was received.</param>
public sealed record EmailResultDto(
    string Id,
    string From,
    string To,
    string Subject,
    string Body,
    DateTimeOffset ReceivedAt);
