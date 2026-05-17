using Hermes.Application.Models.News;
using Hermes.Domain.Entities;

namespace Hermes.Api.Mapping;

internal static class NewsHttpMapper
{
    public static News ToEntity(this CreateNewsRequest dto, int userId) =>
        new()
        {
            UserId = userId,
            Keywords = dto.Keywords,
            Category = dto.Category,
            Languages = dto.Languages,
            Countries = dto.Countries,
            SendOnWeekdays = dto.SendOnWeekdays ?? [],
            SendAtTimes = dto.SendAtTimes ?? [],
            IsEnabled = dto.IsEnabled ?? true,
        };

    public static News ToEntity(this UpdateNewsRequest dto, int userId, News existing) =>
        new()
        {
            Id = dto.Id,
            UserId = userId,
            Keywords = dto.Keywords,
            Category = dto.Category,
            Languages = dto.Languages,
            Countries = dto.Countries,
            SendOnWeekdays = dto.SendOnWeekdays ?? [],
            SendAtTimes = dto.SendAtTimes ?? [],
            NextDigestSlotUtc = existing.NextDigestSlotUtc,
            IsEnabled = dto.IsEnabled ?? existing.IsEnabled,
        };

    public static NewsResponse ToResponse(this News entity) =>
        new()
        {
            Id = entity.Id,
            UserId = entity.UserId,
            Keywords = entity.Keywords,
            Category = entity.Category,
            Languages = entity.Languages,
            Countries = entity.Countries,
            SendOnWeekdays = entity.SendOnWeekdays ?? [],
            SendAtTimes = entity.SendAtTimes ?? [],
            IsEnabled = entity.IsEnabled,
        };
}
