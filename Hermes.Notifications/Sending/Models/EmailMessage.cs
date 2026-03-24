namespace Hermes.Notifications.Sending.Models;

/// <summary>
/// Outgoing e-mail content and routing information.
/// </summary>
/// <param name="To">Primary recipient.</param>
/// <param name="Subject">Message subject.</param>
/// <param name="Body">HTML body.</param>
/// <param name="Attachments">Optional attachments; <c>null</c> or empty means no attachments.</param>
public sealed record EmailMessage(
    EmailRecipient To,
    string Subject,
    string Body,
    IEnumerable<EmailAttachment>? Attachments = null);
