using Hangfire;
using Hermes.Application.Jobs;
using Hermes.Application.Models.Email;
using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Notifications.Receiving.Models;
using Hermes.Worker.MailHog;
using Microsoft.Extensions.Options;

namespace Hermes.Worker.Scheduling;

/// <summary>
/// Minutely Hangfire entry point: loads all news profiles, enqueues <see cref="NotificationJobs.SendNewsDigestAsync"/>
/// once <b>per matching news row</b> (same user, same time, two profiles → two jobs, two e-mails — not merged).
/// </summary>
public sealed class NewsletterScheduler(
    INewsletterScheduleService newsletterScheduleService,
    ILogger<NewsletterScheduler> logger,
    IEmailSender emailSender,
    EmailSettings emailSettings,
    IOptions<MailHogSettings> mailHogOptions)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var slotStartLocal = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Local);
        var slotStartUtc = slotStartLocal.ToUniversalTime();

        logger.LogInformation(
            "[NewsletterScheduler] === Run START === host local now={Local:o} | slot local={SlotLocal:o} | slotUtc={SlotUtc:o} | host TZ={TzId}",
            now,
            slotStartLocal,
            slotStartUtc,
            TimeZoneInfo.Local.Id);

        var due = await newsletterScheduleService.GetDueItemsAsync(now, cancellationToken).ConfigureAwait(false);

        foreach (var (newsId, userId) in due)
        {
            var jobId = BackgroundJob.Enqueue<NotificationJobs>(j =>
                j.SendNewsDigestAsync(userId, newsId, slotStartUtc, CancellationToken.None));
            logger.LogInformation(
                "[NewsletterScheduler] Enqueued NotificationJobs newsId={NewsId} userId={UserId}, Hangfire job id={JobId}.",
                newsId,
                userId,
                jobId);
        }

        logger.LogInformation("[NewsletterScheduler] === Run END === slotUtc={Slot:o} | due jobs={DueCount}", slotStartUtc, due.Count);

        if (mailHogOptions.Value.SendSchedulerTestMailEachMinute)
        {
            try
            {
                await MailHogSchedulerTestMail.SendAsync(
                        emailSender,
                        emailSettings,
                        DateTimeOffset.Now,
                        logger,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[NewsletterScheduler] MailHog-Scheduler-Testmail fehlgeschlagen.");
            }
        }
    }  
}
