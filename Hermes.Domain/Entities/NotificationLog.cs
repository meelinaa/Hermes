using Hermes.Domain.Enums;

namespace Hermes.Domain.Entities;

public class NotificationLog
{
    // User
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    // Infos
    public DateTime SentAt { get; set; }
    public NotificationStatus Status { get; set; } 
    public DeliveryChannel Channel { get; set; } 
    public string? ErrorMessage { get; set; } 
    public int RetryCount { get; set; } = 0;
    public DateTime? NextRetryAt { get; set; }   
}
