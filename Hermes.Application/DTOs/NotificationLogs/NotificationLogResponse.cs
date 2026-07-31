using Hermes.Domain.Enums;

namespace Hermes.Application.DTOs.NotificationLogs;

public sealed record NotificationLogResponse
{
    public int Id { get; init; }

    public int UserId { get; init; }

    public int? NewsId { get; init; }

    public DateTime SentAt { get; init; }

    public NotificationStatus Status { get; init; }

    public DeliveryChannel Channel { get; init; }

    public string? ErrorMessage { get; init; }

    public int RetryCount { get; init; }

    public DateTime? NextRetryAt { get; init; }
}
