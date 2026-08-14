using Hangfire;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services.Newsletter;
using Hermes.Application.Services.NotificationLogs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hermes.Domain.ValueObjects;
using Hermes.Worker.Logging;

namespace Hermes.Worker.Services.Scheduling;

/// <summary>
/// Hangfire minutely tick service: resolves due newsletter subscriptions (UTC slot wall clock) and enqueues one digest job per row.
/// </summary>
public sealed class NewsletterSchedulerWorkerService(
    INewsletterScheduleService newsletterScheduleService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<NewsletterSchedulerWorkerService> logger,
    IOptions<NewsletterOptions> newsletterOptions)
{
    private readonly TimeZoneInfo _newsletterTimeZone =
        NewsletterSchedulingProvider.ResolveTimeZone(newsletterOptions.Value.TimeZoneId);

    /// <summary>
    /// Executes the minutely scheduling loop to resolve due newsletter items and enqueue Hangfire notification jobs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous execution.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        DateTime wallNow = NewsletterSchedulingProvider.GetWallClockNow(_newsletterTimeZone);
        DateTime slotStartWall = NewsletterSchedulingProvider.GetWallClockMinuteStart(_newsletterTimeZone);
        DateTime slotStartUtc = NewsletterSchedulingProvider.ConvertWallMinuteStartToUtc(slotStartWall, _newsletterTimeZone);

        logger.LogRunStart(_newsletterTimeZone.Id, wallNow, slotStartWall, slotStartUtc);

        DateTime slotEndUtc = slotStartUtc.AddMinutes(1);

        IReadOnlyList<(NewsletterId NewsId, UserId UserId)> due = await newsletterScheduleService
            .GetDueItemsAsync(wallNow, slotStartUtc, slotEndUtc, cancellationToken)
            .ConfigureAwait(false);

        if (due.Count > 0)
        {
            logger.LogFoundDueItems(due.Count, string.Join(", ", due.Select(d => d.NewsId.Value)));
        }

        foreach ((NewsletterId newsId, UserId userId) in due)
        {
            string? jobId = backgroundJobClient.Enqueue<NotificationJobService>(notificationJobs =>
                notificationJobs.SendNewsDigestAsync(userId, newsId, slotStartUtc, CancellationToken.None));
            logger.LogJobEnqueued(newsId.Value, userId.Value, jobId);
        }

        logger.LogRunEnd(slotStartUtc, due.Count);
    }
}
