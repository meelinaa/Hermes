using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;

public class NotificationLog
{
    public int Id { get; private set; }
    public UserId UserId { get; private set; }

    /// <summary>Optional link to the news profile for digest-related sends.</summary>
    public NewsletterId? NewsId { get; private set; }
    public DateTime SentAt { get; private set; }
    public NotificationStatus Status { get; private set; }
    public DeliveryChannel Channel { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; } = 0;
    public DateTime? NextRetryAt { get; private set; }

    // EF Core Konstruktor
    private NotificationLog() { }

    public static NotificationLog Create(UserId userId, DeliveryChannel channel, DateTime sentAt, NewsletterId? newsId = null)
    {
        return new NotificationLog
        {
            UserId = userId,
            Channel = channel,
            NewsId = newsId,
            Status = NotificationStatus.Pending,
            SentAt = sentAt
        };
    }

    public void MarkAsFailed(string error, DateTime? nextRetry)
    {
        Status = NotificationStatus.Failed;
        ErrorMessage = error;
        NextRetryAt = nextRetry;
        RetryCount++;
    }

    public void MarkAsSent()
    {
        Status = NotificationStatus.Sent;
        ErrorMessage = null;
        NextRetryAt = null;
    }
}

