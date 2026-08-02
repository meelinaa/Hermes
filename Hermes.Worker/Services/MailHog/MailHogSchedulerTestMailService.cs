using Hermes.Application.DTOs.Email;
using Hermes.Application.Options;
using Hermes.Application.Ports.Outbound;
using Microsoft.Extensions.Logging;

namespace Hermes.Worker.Services.MailHog;

/// <summary>
/// Development service for sending test emails to MailHog during scheduled worker runs.
/// </summary>
public static class MailHogSchedulerTestMailService
{
    /// <summary>
    /// Sends a diagnostic test email to verify SMTP configuration and MailHog connectivity.
    /// </summary>
    /// <param name="emailSender">The outbound email provider implementation.</param>
    /// <param name="smtp">The configured email options.</param>
    /// <param name="schedulerRunAt">The timestamp when the scheduler ran.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    public static async Task SendAsync(
        IEmailProvider emailSender,
        EmailOptions smtp,
        DateTimeOffset schedulerRunAt,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        EmailRecipientDto to = new(smtp.DefaultFromAddress, smtp.DefaultFromName);
        string body =
            $"<p>Hermes Worker – Scheduler-Lauf (MailHog-Test)</p>" +
            $"<p>Wandzeit (Konfig Newsletter-TZ): {schedulerRunAt.DateTime:O}<br/>UTC: {schedulerRunAt.UtcDateTime:O}</p>" +
            "<p>Wenn du das in MailHog siehst, ist SMTP ok.</p>";

        await emailSender.SendAsync(
                new EmailMessageDto(
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
