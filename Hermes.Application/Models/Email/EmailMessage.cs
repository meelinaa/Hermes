namespace Hermes.Application.Models.Email;

public sealed record EmailMessage(
    EmailRecipient To,
    string Subject,
    string Body,
    IEnumerable<EmailAttachment>? Attachments = null);
