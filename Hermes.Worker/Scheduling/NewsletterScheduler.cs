using Hangfire;
using Hermes.Application.Jobs;
using Hermes.Application.Models.Email;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Scheduling;
using Hermes.Application.Services;
using Hermes.Notifications.Receiving.Models;
using Hermes.Worker.MailHog;
using Microsoft.Extensions.Options;

namespace Hermes.Worker.Scheduling;

/// <summary>
/// Minutely Hangfire entry point: resolves due <c>news</c> rows for the current wall-clock minute (materialized UTC slot and/or JSON schedule),
/// enqueues <see cref="NotificationJobs.SendNewsDigestAsync"/> once per due row.
/// </summary>
public sealed class NewsletterScheduler(
    INewsletterScheduleService newsletterScheduleService,
    ILogger<NewsletterScheduler> logger,
    IEmailSender emailSender,
    EmailSettings emailSettings,
    IOptions<MailHogSettings> mailHogOptions,
    IOptions<NewsletterOptions> newsletterOptions)
{
    private readonly TimeZoneInfo _newsletterTimeZone =
        NewsletterSchedulingClock.ResolveTimeZone(newsletterOptions.Value.TimeZoneId);

    /// <summary>Evaluates due newsletter items for the current minute and enqueues one Hangfire job per due row.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        DateTime wallNow = NewsletterSchedulingClock.GetWallClockNow(_newsletterTimeZone);
        DateTime slotStartWall = NewsletterSchedulingClock.GetWallClockMinuteStart(_newsletterTimeZone);
        DateTime slotStartUtc = NewsletterSchedulingClock.WallMinuteStartToUtc(slotStartWall, _newsletterTimeZone);
        DateTimeOffset wallStamp = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _newsletterTimeZone);

        logger.LogInformation(
            "[NewsletterScheduler] === Run START === wall-now (newsletter TZ={TzId})={Wall:o} | minute start wall={SlotWall:o} | slotUtc={SlotUtc:o} | source=UtcNow→TZ",
            _newsletterTimeZone.Id,
            wallNow,
            slotStartWall,
            slotStartUtc);

        DateTime slotEndUtc = slotStartUtc.AddMinutes(1);

        IReadOnlyList<(int NewsId, int UserId)> due = await newsletterScheduleService
            .GetDueItemsAsync(wallNow, slotStartUtc, slotEndUtc, cancellationToken)
            .ConfigureAwait(false);

        foreach ((int newsId, int userId) in due)
        {
            string? jobId = BackgroundJob.Enqueue<NotificationJobs>(notificationJobs =>
                notificationJobs.SendNewsDigestAsync(userId, newsId, slotStartUtc, CancellationToken.None));
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
                        wallStamp,
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
