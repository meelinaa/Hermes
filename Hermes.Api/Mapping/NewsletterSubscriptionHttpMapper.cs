using Hermes.Application.Models.NewsletterSubscription;
using Hermes.Domain.Entities;

namespace Hermes.Api.Mapping;

/// <summary>
/// Static mapper class converting newsletter subscription DTOs to domain entities and vice versa.
/// </summary>
internal static class NewsletterSubscriptionHttpMapper
{
    /// <summary>
    /// Converts a <see cref="CreateNewsletterSubscriptionRequest"/> DTO to a domain entity.
    /// </summary>
    public static NewsletterSubscription ToEntity(this CreateNewsletterSubscriptionRequest dto, int userId) =>
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

    /// <summary>
    /// Converts an <see cref="UpdateNewsletterSubscriptionRequest"/> DTO to a domain entity.
    /// </summary>
    public static NewsletterSubscription ToEntity(this UpdateNewsletterSubscriptionRequest dto, int userId, NewsletterSubscription existing) =>
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

    /// <summary>
    /// Converts a <see cref="NewsletterSubscription"/> domain entity to a response DTO.
    /// </summary>
    public static NewsletterSubscriptionResponse ToResponse(this NewsletterSubscription entity) =>
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
