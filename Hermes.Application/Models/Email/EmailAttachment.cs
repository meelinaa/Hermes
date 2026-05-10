namespace Hermes.Application.Models.Email;

public sealed record EmailAttachment(string FileName, Stream Content, string ContentType);
