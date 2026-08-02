using System.Net;
using System.Net.Mail;
using System.Text;
using Hermes.Application.DTOs.Email;
using Hermes.Application.Options.Email;
using Hermes.Application.Ports.Outbound;

namespace Hermes.Notifications.Sending.Providers;

/// <summary>
/// Sends e-mail via <see cref="SmtpClient"/> using <see cref="EmailOptions"/>.
/// </summary>
/// <param name="settings">The configured email options.</param>
public sealed class SmtpEmailProvider(EmailOptions settings) : IEmailProvider
{
    /// <summary>
    /// Sends an e-mail message via SMTP using configured sender defaults.
    /// </summary>
    /// <param name="message">The email message DTO containing recipient, subject, body, and attachments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public async Task SendAsync(EmailMessageDto message, CancellationToken cancellationToken = default)
    {
        using SmtpClient smtp = CreateSmtpClient();
        using MailMessage mail = CreateMailMessage(message);
        await smtp.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates and configures an SMTP client instance from configured settings.
    /// </summary>
    /// <returns>A configured SmtpClient instance.</returns>
    private SmtpClient CreateSmtpClient()
    {
        SmtpClient client = new(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);

        return client;
    }

    /// <summary>
    /// Builds a mail message with headers, reply-to, and optional attachments.
    /// </summary>
    /// <param name="message">The email message DTO.</param>
    /// <returns>A populated MailMessage object.</returns>
    private MailMessage CreateMailMessage(EmailMessageDto message)
    {
        MailAddress from = new(settings.DefaultFromAddress, settings.DefaultFromName);
        MailAddress to = new(message.To.Address, message.To.DisplayName ?? string.Empty);

        MailMessage mail = new(from, to)
        {
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = true,
            Priority = MailPriority.Normal,
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8,
            HeadersEncoding = Encoding.UTF8
        };

        mail.Headers.Add("X-Mailer", settings.XMailer);
        mail.ReplyToList.Add(new MailAddress(settings.DefaultReplyToAddress, settings.DefaultReplyToName));

        if (message.Attachments is not null)
        {
            foreach (EmailAttachmentDto attachment in message.Attachments)
                mail.Attachments.Add(new Attachment(attachment.Content, attachment.FileName, attachment.ContentType));
        }

        return mail;
    }
}
