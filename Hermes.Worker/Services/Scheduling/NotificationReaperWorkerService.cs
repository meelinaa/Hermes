using Hermes.Application.Ports.Outbound;
using Microsoft.Extensions.Logging;

namespace Hermes.Worker.Services.Scheduling;

/// <summary>
/// Background worker service that reaps abandoned, stale pending notification records.
/// Prevents stuck-pending states from causing silent digest loss.
/// </summary>
public sealed class NotificationReaperWorkerService(
    INotificationLogRepository notificationLogs,
    ILogger<NotificationReaperWorkerService> logger)
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Executes the cleanup cycle to transition stale pending logs older than 5 minutes to Failed.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A Task representing the asynchronous execution.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting stale pending notification reaper cycle (threshold: {Threshold}).", StaleThreshold);

        int reapedCount = await notificationLogs
            .ReapStalePendingNotificationsAsync(StaleThreshold, cancellationToken)
            .ConfigureAwait(false);

        if (reapedCount > 0)
        {
            logger.LogWarning("Reaped {Count} stale pending notifications.", reapedCount);
        }
        else
        {
            logger.LogDebug("No stale pending notifications found during reaper cycle.");
        }
    }
}
