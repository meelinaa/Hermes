using Hermes.Notifications.Sending.Models;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Hermes.Notifications.Sending;

/// <summary>
/// Sends e-mail via <see cref="SmtpClient"/> using <see cref="EmailSettings"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="SmtpEmailSender"/>.
/// </remarks>
/// <param name="settings">SMTP and default envelope configuration.</param>
public sealed class SmtpEmailSender(EmailSettings settings) : IEmailSender
{

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        using var smtp = CreateSmtpClient();
        using var mail = CreateMailMessage(message);
        await smtp.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
    }

    private SmtpClient CreateSmtpClient()
    {
        var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        return client;
    }

    private MailMessage CreateMailMessage(EmailMessage message)
    {
        var from = new MailAddress(settings.DefaultFromAddress, settings.DefaultFromName);
        var to = new MailAddress(message.To.Address, message.To.DisplayName ?? string.Empty);

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
            foreach (var attachment in message.Attachments)
            {
                mail.Attachments.Add(new Attachment(attachment.Content, attachment.FileName, attachment.ContentType));
            }
        }

        return mail;
    }
}
