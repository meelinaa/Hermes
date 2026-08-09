using Microsoft.Extensions.Logging;

namespace Hermes.Worker.Logging;

public static partial class SchedulerLogs
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[NewsletterSchedulerWorkerService] === Run START === wall-now (newsletter TZ={TzId})={Wall:o} | minute start wall={SlotWall:o} | slotUtc={SlotUtc:o} | source=UtcNow→TZ")]
    public static partial void LogRunStart(this ILogger logger, string tzId, DateTime wall, DateTime slotWall, DateTime slotUtc);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "[NewsletterSchedulerWorkerService] Found {Count} due newsletter subscription items. Enqueuing jobs for SubscriptionIds: {SubscriptionIds}")]
    public static partial void LogFoundDueItems(this ILogger logger, int count, string subscriptionIds);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "[NewsletterSchedulerWorkerService] Enqueued NotificationJobService newsId={NewsId} userId={UserId}, Hangfire job id={JobId}.")]
    public static partial void LogJobEnqueued(this ILogger logger, int newsId, int userId, string jobId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "[NewsletterSchedulerWorkerService] === Run END === slotUtc={Slot:o} | due jobs={DueCount}")]
    public static partial void LogRunEnd(this ILogger logger, DateTime slot, int dueCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "[NewsletterSchedulerWorkerService] Testmail-Versand aufgrund von Cancellation abgebrochen.")]
    public static partial void LogTestMailCanceled(this ILogger logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "[NewsletterSchedulerWorkerService] MailHog-Scheduler-Testmail fehlgeschlagen.")]
    public static partial void LogTestMailFailed(this ILogger logger, Exception ex);
}
