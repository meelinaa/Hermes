namespace Hermes.Notifications.Sending.Models;

/// <summary>
/// An attachment to include with an outgoing e-mail. The caller owns <see cref="Content"/> until send completes;
/// after <see cref="IEmailSender.SendAsync"/>, streams may be disposed by the mail implementation.
/// </summary>
/// <param name="FileName">File name shown to recipients.</param>
/// <param name="Content">Attachment payload.</param>
/// <param name="ContentType">MIME type (e.g. <c>application/pdf</c>).</param>
public sealed record EmailAttachment(string FileName, Stream Content, string ContentType);
