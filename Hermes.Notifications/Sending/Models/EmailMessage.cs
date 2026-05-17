namespace Hermes.Notifications.Sending.Models;

public sealed record EmailMessage(
    EmailRecipient To,
    string Subject,
    string Body,
    IEnumerable<EmailAttachment>? Attachments = null);
