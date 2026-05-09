using Hermes.Application.Models.NotificationLogs;
using Hermes.Domain.Entities;

namespace Hermes.Api.Mapping;

internal static class NotificationLogHttpMapper
{
    public static NotificationLog ToEntity(this CreateNotificationLogRequest dto, int userId) =>
        new()
        {
            UserId = userId,
            NewsId = dto.NewsId,
            SentAt = dto.SentAt,
            Status = dto.Status,
            Channel = dto.Channel,
            ErrorMessage = dto.ErrorMessage,
            RetryCount = dto.RetryCount,
            NextRetryAt = dto.NextRetryAt,
        };

    public static NotificationLogResponse ToResponse(this NotificationLog entity) =>
        new()
        {
            Id = entity.Id,
            UserId = entity.UserId,
            NewsId = entity.NewsId,
            SentAt = entity.SentAt,
            Status = entity.Status,
            Channel = entity.Channel,
            ErrorMessage = entity.ErrorMessage,
            RetryCount = entity.RetryCount,
            NextRetryAt = entity.NextRetryAt,
        };
}
