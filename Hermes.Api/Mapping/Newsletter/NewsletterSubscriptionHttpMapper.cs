using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Domain.Entities;

namespace Hermes.Api.Mapping.Newsletter;

/// <summary>
/// Static mapper class converting newsletter subscription DTOs to domain entities and vice versa.
/// </summary>
internal static class NewsletterSubscriptionHttpMapper
{
    /// <summary>
    /// Converts a <see cref="CreateNewsletterSubscriptionRequestDto"/> DTO to a domain entity.
    /// </summary>
    /// <param name="dto">The creation request payload.</param>
    /// <param name="userId">The ID of the subscribing user.</param>
    /// <returns>The mapped <see cref="NewsletterSubscription"/> domain entity.</returns>
    public static NewsletterSubscription ToEntity(this CreateNewsletterSubscriptionRequestDto dto, int userId) =>
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
    /// Converts an <see cref="UpdateNewsletterSubscriptionRequestDto"/> DTO to a domain entity.
    /// </summary>
    /// <param name="dto">The update request payload.</param>
    /// <param name="userId">The ID of the user requesting update.</param>
    /// <param name="existing">The existing subscription entity.</param>
    /// <returns>The updated <see cref="NewsletterSubscription"/> entity.</returns>
    public static NewsletterSubscription ToEntity(this UpdateNewsletterSubscriptionRequestDto dto, int userId, NewsletterSubscription existing) =>
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
    /// <param name="entity">The subscription domain entity.</param>
    /// <returns>The mapped <see cref="NewsletterSubscriptionResponseDto"/>.</returns>
    public static NewsletterSubscriptionResponseDto ToResponse(this NewsletterSubscription entity) =>
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
