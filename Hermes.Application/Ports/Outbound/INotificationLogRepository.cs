using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Outbound port for persisting, querying, reserving, and updating notification audit logs.
/// </summary>
public interface INotificationLogRepository
{
    /// <summary>
    /// Persists a new notification log entity.
    /// </summary>
    /// <param name="log">The log entity to insert.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing notification log entity (e.g. status transitions).
    /// </summary>
    /// <param name="log">The log entity with updated values.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    ValueTask UpdateNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to atomically reserve a notification delivery slot or reclaim an expired pending lease.
    /// </summary>
    /// <param name="log">The proposed pending notification log entry.</param>
    /// <param name="leaseDuration">The duration after which an uncompleted pending log is eligible for reclaim.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome of the slot reservation.</returns>
    ValueTask<SlotReservationResult> TryReserveSlotAsync(NotificationLog log, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reaps stale uncompleted pending notification logs and marks them as failed.
    /// </summary>
    /// <param name="olderThan">The threshold duration beyond which pending entries are considered abandoned.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of reaped log entries.</returns>
    ValueTask<int> ReapStalePendingNotificationsAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a notification log by its ID.
    /// </summary>
    /// <param name="log">The log containing the search ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The notification log or null if not found.</returns>
    ValueTask<NotificationLog?> GetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a successfully sent notification log exists for the given user, newsletter, and time window.
    /// </summary>
    /// <param name="userId">The target user ID.</param>
    /// <param name="newsId">The newsletter ID.</param>
    /// <param name="windowStartUtc">Window start timestamp.</param>
    /// <param name="windowEndUtc">Window end timestamp.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True if already sent in window; otherwise false.</returns>
    ValueTask<bool> ExistsSentNotificationInWindowAsync(UserId userId, NewsletterId newsId, DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken cancellationToken = default);
}
