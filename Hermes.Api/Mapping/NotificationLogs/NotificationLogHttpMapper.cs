using Hermes.Application.DTOs.NotificationLogs;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Api.Mapping.NotificationLogs;

/// <summary>
/// Static mapper class converting notification log DTOs to domain entities and vice versa.
/// </summary>
internal static class NotificationLogHttpMapper
{
    /// <summary>
    /// Converts a <see cref="CreateNotificationLogRequestDto"/> DTO into a <see cref="NotificationLog"/> entity.
    /// </summary>
    /// <param name="dto">The notification log creation payload.</param>
    /// <param name="userId">The ID of the owning user.</param>
    /// <returns>The mapped <see cref="NotificationLog"/> entity.</returns>
    public static NotificationLog ToEntity(this CreateNotificationLogRequestDto dto, int userId) =>
        new()
        {
            UserId = new UserId(userId),
            NewsId = dto.NewsId.HasValue ? new NewsletterId(dto.NewsId.Value) : null,
            SentAt = dto.SentAt,
            Status = dto.Status,
            Channel = dto.Channel,
            ErrorMessage = dto.ErrorMessage,
            RetryCount = dto.RetryCount,
            NextRetryAt = dto.NextRetryAt,
        };

    /// <summary>
    /// Converts a <see cref="NotificationLog"/> domain entity into a <see cref="NotificationLogResponseDto"/>.
    /// </summary>
    /// <param name="entity">The notification log entity.</param>
    /// <returns>The mapped <see cref="NotificationLogResponseDto"/>.</returns>
    public static NotificationLogResponseDto ToResponse(this NotificationLog entity) =>
        new()
        {
            Id = entity.Id,
            UserId = entity.UserId.Value,
            NewsId = entity.NewsId?.Value,
            SentAt = entity.SentAt,
            Status = entity.Status,
            Channel = entity.Channel,
            ErrorMessage = entity.ErrorMessage,
            RetryCount = entity.RetryCount,
            NextRetryAt = entity.NextRetryAt,
        };
}
