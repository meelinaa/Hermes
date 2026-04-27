using Hermes.Application.Models.Email;
using Hermes.Application.Ports;

namespace Hermes.Worker.MailHog;

/// <summary>
/// Sends a minimal HTML message through the configured SMTP (e.g. MailHog on port 1025) after a scheduler tick.
/// </summary>
public static class MailHogSchedulerTestMail
{
    /// <summary>
    /// Delivers a test mail to <paramref name="smtp"/>.<see cref="EmailSettings.DefaultFromAddress"/> (MailHog accepts any recipient).
    /// </summary>
    public static async Task SendAsync(
        IEmailSender emailSender,
        EmailSettings smtp,
        DateTimeOffset schedulerRunAt,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var to = new EmailRecipient(smtp.DefaultFromAddress, smtp.DefaultFromName);
        var body =
            $"<p>Hermes Worker – Scheduler-Lauf (MailHog-Test)</p>" +
            $"<p>Lokal: {schedulerRunAt.LocalDateTime:O}<br/>UTC: {schedulerRunAt.UtcDateTime:O}</p>" +
            "<p>Wenn du das in MailHog siehst, ist SMTP ok.</p>";

        await emailSender.SendAsync(
                new EmailMessage(
                    to,
                    $"[Hermes/MailHog] Scheduler-Test {schedulerRunAt.LocalDateTime:HH:mm:ss}",
                    body),
                cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation(
            "[MailHog] Scheduler-Testmail gesendet an {Address} (Absender wie in Email:DefaultFromAddress).",
            smtp.DefaultFromAddress);
    }
}
