using Hermes.Domain.Enums;

namespace Hermes.Domain.Entities;

public class NotificationLog
{
    // User
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>EF navigation; optional on JSON-bound instances (API body omits this).</summary>
    public User? User { get; set; }

    /// <summary>When set, this log row refers to a single <see cref="News"/> digest send (one e-mail per news profile).</summary>
    public int? NewsId { get; set; }

    // Infos
    public DateTime SentAt { get; set; }
    public NotificationStatus Status { get; set; } 
    public DeliveryChannel Channel { get; set; } 
    public string? ErrorMessage { get; set; } 
    public int RetryCount { get; set; } = 0;
    public DateTime? NextRetryAt { get; set; }   
}
