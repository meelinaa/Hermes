namespace Hermes.Application.DTOs.Email;

public sealed record EmailMessageDto(
    EmailRecipientDto To,
    string Subject,
    string Body,
    IEnumerable<EmailAttachmentDto>? Attachments = null);
