namespace Hermes.Application.DTOs.Email;

public sealed record EmailAttachment(string FileName, Stream Content, string ContentType);
