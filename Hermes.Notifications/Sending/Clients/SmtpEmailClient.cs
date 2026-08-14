using System.Threading;
using System.Threading.Tasks;
using Hermes.Application.DTOs.Email;
using Hermes.Application.Options.Email;
using Hermes.Application.Ports.Outbound;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Polly;
using Polly.Registry;

namespace Hermes.Notifications.Sending.Providers;

/// <summary>
/// Sends e-mail via MailKit <see cref="SmtpClient"/> using <see cref="EmailOptions"/>.
/// </summary>
/// <param name="settings">The configured email options.</param>
/// <param name="pipelineProvider">The Polly resilience pipeline provider.</param>
public sealed class SmtpEmailClient(EmailOptions settings, ResiliencePipelineProvider<string> pipelineProvider) : IEmailProvider
{
    private readonly ResiliencePipeline _pipeline = pipelineProvider.GetPipeline("smtp-retry");

    /// <inheritdoc />
    public async Task SendAsync(EmailMessageDto message, CancellationToken cancellationToken = default)
    {
        await _pipeline.ExecuteAsync(async ct =>
        {
            using MimeMessage mail = CreateMimeMessage(message);
            using SmtpClient smtp = new();
            
            SecureSocketOptions secureSocketOptions = settings.EnableSsl 
                ? SecureSocketOptions.Auto 
                : SecureSocketOptions.None;

            await smtp.ConnectAsync(settings.Host, settings.Port, secureSocketOptions, ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                await smtp.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, ct).ConfigureAwait(false);
            }

            await smtp.SendAsync(mail, ct).ConfigureAwait(false);
            await smtp.DisconnectAsync(true, ct).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a MimeKit message with headers, reply-to, and optional attachments.
    /// </summary>
    /// <param name="message">The email message DTO.</param>
    /// <returns>A populated MimeMessage object.</returns>
    private MimeMessage CreateMimeMessage(EmailMessageDto message)
    {
        MimeMessage mail = new();
        mail.From.Add(new MailboxAddress(settings.DefaultFromName, settings.DefaultFromAddress));
        mail.To.Add(new MailboxAddress(message.To.DisplayName ?? string.Empty, message.To.Address));
        
        mail.Subject = message.Subject;
        mail.Headers.Add("X-Mailer", settings.XMailer);
        mail.ReplyTo.Add(new MailboxAddress(settings.DefaultReplyToName, settings.DefaultReplyToAddress));

        var builder = new BodyBuilder
        {
            HtmlBody = message.Body
        };

        if (message.Attachments is not null)
        {
            foreach (EmailAttachmentDto attachment in message.Attachments)
            {
                builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }
        }

        mail.Body = builder.ToMessageBody();

        return mail;
    }
}
