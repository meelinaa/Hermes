using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Domain.Entities;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Defines the data access port for managing newsletter subscriptions.
/// </summary>
public interface INewsletterSubscriptionRepository
{
    /// <summary>
    /// Persists a new newsletter subscription in the store.
    /// </summary>
    ValueTask SetNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing newsletter subscription in the store.
    /// </summary>
    ValueTask UpdateNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified newsletter subscription from the store.
    /// </summary>
    ValueTask DeleteNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paged list of newsletter subscriptions matching the query parameters.
    /// </summary>
    ValueTask<NewsletterSubscriptionListResultDto> GetNewsListAsync(NewsletterSubscriptionListQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves newsletter subscription schedules that are due for delivery in the specified slot.
    /// </summary>
    ValueTask<List<(NewsletterId NewsId, UserId UserId)>> GetDueNewsScheduleForSlotAsync(
        Weekdays weekday,
        int hour,
        int minute,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the next digest slot timestamp for a newsletter subscription.
    /// </summary>
    ValueTask AdvanceNextDigestSlotAsync(
        NewsletterId newsId,
        UserId userId,
        TimeZoneInfo newsletterTimeZone,
        DateTime referenceUtcExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a newsletter subscription by ID for a specific user.
    /// </summary>
    ValueTask<NewsletterSubscription?> GetNewsByIdAsync(UserId userId, NewsletterId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a newsletter subscription by its ID.
    /// </summary>
    ValueTask<NewsletterSubscription?> FindNewsByIdAsync(NewsletterId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all newsletter subscriptions belonging to a user.
    /// </summary>
    ValueTask<int> DeleteAllNewsByUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
