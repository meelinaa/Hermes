namespace Hermes.Application.DTOs.Email;

public sealed record EmailAttachmentDto(string FileName, Stream Content, string ContentType);
