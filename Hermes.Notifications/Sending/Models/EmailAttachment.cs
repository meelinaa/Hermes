namespace Hermes.Notifications.Sending.Models;

public sealed record EmailAttachment(string FileName, Stream Content, string ContentType);
