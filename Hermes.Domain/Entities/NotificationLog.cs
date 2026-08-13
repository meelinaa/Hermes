using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;

public class NotificationLog
{
    public int Id { get; set; }
    public UserId UserId { get; set; }

    /// <summary>Optional link to the news profile for digest-related sends.</summary>
    public NewsletterId? NewsId { get; set; }
    public DateTime SentAt { get; set; }
    public NotificationStatus Status { get; set; }
    public DeliveryChannel Channel { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? NextRetryAt { get; set; }
}
