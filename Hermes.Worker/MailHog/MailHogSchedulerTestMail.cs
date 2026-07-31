using Hermes.Application.DTOs.Email;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;

namespace Hermes.Worker.MailHog;

public static class MailHogSchedulerTestMail
{
    public static async Task SendAsync(
        IEmailSender emailSender,
        EmailSettings smtp,
        DateTimeOffset schedulerRunAt,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        EmailRecipient to = new(smtp.DefaultFromAddress, smtp.DefaultFromName);
        string body =
            $"<p>Hermes Worker – Scheduler-Lauf (MailHog-Test)</p>" +
            $"<p>Wandzeit (Konfig Newsletter-TZ): {schedulerRunAt.DateTime:O}<br/>UTC: {schedulerRunAt.UtcDateTime:O}</p>" +
            "<p>Wenn du das in MailHog siehst, ist SMTP ok.</p>";

        await emailSender.SendAsync(
                new EmailMessage(
                    to,
                    $"[Hermes/MailHog] Scheduler-Test {schedulerRunAt.DateTime:HH:mm:ss}",
                    body),
                cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "[MailHog] Scheduler-Testmail gesendet an {Address} (Absender wie in Email:DefaultFromAddress).",
            smtp.DefaultFromAddress);
    }
}
