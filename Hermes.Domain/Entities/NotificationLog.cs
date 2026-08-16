using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;

/// <summary>
/// Domain entity representing a dispatched or pending notification delivery attempt (e.g., email digest, verification mail).
/// Tracks delivery status, retry attempts, scheduled slot timestamps, and error diagnostics.
/// </summary>
public class NotificationLog
{
    /// <summary>
    /// Gets the unique surrogate identifier of the notification log.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// Gets the unique identifier of the recipient user.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the optional newsletter subscription identifier for digest deliveries.
    /// </summary>
    public NewsletterId? NewsId { get; private set; }

    /// <summary>
    /// Gets the immutable scheduled delivery slot timestamp in UTC.
    /// Used as the primary deduplication anchor for unique constraint enforcement.
    /// </summary>
    public DateTime? ScheduledSlotUtc { get; private set; }

    /// <summary>
    /// Gets the actual timestamp when the notification was successfully transmitted via the delivery channel.
    /// </summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>
    /// Gets the current delivery lifecycle status (Pending, Sent, Failed).
    /// </summary>
    public NotificationStatus Status { get; private set; }

    /// <summary>
    /// Gets the channel used for notification transmission (e.g. Email).
    /// </summary>
    public DeliveryChannel Channel { get; private set; }

    /// <summary>
    /// Gets diagnostic error details if transmission failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Gets the number of delivery attempt retries executed for this log entry.
    /// </summary>
    public int RetryCount { get; private set; } = 0;

    /// <summary>
    /// Gets the optional timestamp for the next scheduled retry attempt.
    /// </summary>
    public DateTime? NextRetryAt { get; private set; }

    /// <summary>
    /// Gets the creation timestamp in UTC.
    /// Used for lease expiration and stale pending detection.
    /// </summary>
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Private constructor required for EF Core materialization.
    /// </summary>
    private NotificationLog() { }

    /// <summary>
    /// Factory method for creating a new notification log in the <see cref="NotificationStatus.Pending"/> state.
    /// </summary>
    /// <param name="userId">The ID of the target user.</param>
    /// <param name="channel">The delivery channel to utilize.</param>
    /// <param name="scheduledSlotUtc">The target delivery slot timestamp in UTC.</param>
    /// <param name="newsId">Optional newsletter subscription ID.</param>
    /// <param name="createdAtUtc">Optional creation timestamp in UTC (defaults to UtcNow).</param>
    /// <returns>A new <see cref="NotificationLog"/> instance.</returns>
    public static NotificationLog Create(
        UserId userId,
        DeliveryChannel channel,
        DateTime? scheduledSlotUtc,
        NewsletterId? newsId = null,
        DateTime? createdAtUtc = null)
    {
        return new NotificationLog
        {
            UserId = userId,
            Channel = channel,
            NewsId = newsId,
            ScheduledSlotUtc = scheduledSlotUtc,
            Status = NotificationStatus.Pending,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Marks the notification as failed with the specified error message and next retry schedule.
    /// </summary>
    /// <param name="error">The diagnostic failure description.</param>
    /// <param name="nextRetry">The optional timestamp for the next retry.</param>
    public void MarkAsFailed(string error, DateTime? nextRetry = null)
    {
        Status = NotificationStatus.Failed;
        ErrorMessage = error;
        NextRetryAt = nextRetry;
        RetryCount++;
    }

    /// <summary>
    /// Marks the notification as successfully sent at the specified timestamp.
    /// </summary>
    /// <param name="sentAtUtc">The UTC timestamp of successful transmission.</param>
    public void MarkAsSent(DateTime? sentAtUtc = null)
    {
        Status = NotificationStatus.Sent;
        SentAt = sentAtUtc ?? ScheduledSlotUtc ?? DateTime.UtcNow;
        ErrorMessage = null;
        NextRetryAt = null;
    }

    /// <summary>
    /// Reclaims an existing pending lease during a worker retry or recovery cycle.
    /// Refreshes the lease creation timestamp to prevent premature reaping.
    /// </summary>
    /// <param name="reclaimedAtUtc">The UTC timestamp when the lease was reclaimed.</param>
    public void ReclaimLease(DateTime reclaimedAtUtc)
    {
        CreatedAtUtc = reclaimedAtUtc;
        RetryCount++;
    }
}
