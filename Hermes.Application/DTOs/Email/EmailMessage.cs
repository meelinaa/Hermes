namespace Hermes.Application.DTOs.Email;

public sealed record EmailMessage(
    EmailRecipient To,
    string Subject,
    string Body,
    IEnumerable<EmailAttachment>? Attachments = null);
