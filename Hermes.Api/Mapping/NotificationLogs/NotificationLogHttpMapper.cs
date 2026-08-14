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
    public static NotificationLog ToEntity(this CreateNotificationLogRequestDto dto, int userId)
    {
        var log = NotificationLog.Create(
            new UserId(userId),
            dto.Channel,
            dto.SentAt,
            dto.NewsId.HasValue ? new NewsletterId(dto.NewsId.Value) : null);

        // Reflection is used here because the domain entity is properly encapsulated
        // and an API endpoint directly injecting state bypasses domain rules.
        typeof(NotificationLog).GetProperty("Status")!.SetValue(log, dto.Status);
        typeof(NotificationLog).GetProperty("ErrorMessage")!.SetValue(log, dto.ErrorMessage);
        typeof(NotificationLog).GetProperty("RetryCount")!.SetValue(log, dto.RetryCount);
        typeof(NotificationLog).GetProperty("NextRetryAt")!.SetValue(log, dto.NextRetryAt);

        return log;
    }

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
