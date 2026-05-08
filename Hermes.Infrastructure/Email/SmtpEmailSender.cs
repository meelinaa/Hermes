using Hermes.Application.Models.Email;
using Hermes.Application.Ports;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Hermes.Infrastructure.Email;

/// <summary>
/// Sends e-mail via <see cref="SmtpClient"/> using <see cref="EmailSettings"/>.
/// </summary>
public sealed class SmtpEmailSender(EmailSettings settings) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        using SmtpClient smtp = CreateSmtpClient();
        using MailMessage mail = CreateMailMessage(message);
        await smtp.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
    }

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

    private MailMessage CreateMailMessage(EmailMessage message)
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
            foreach (EmailAttachment attachment in message.Attachments)
                mail.Attachments.Add(new Attachment(attachment.Content, attachment.FileName, attachment.ContentType));
        }

        return mail;
    }
}
