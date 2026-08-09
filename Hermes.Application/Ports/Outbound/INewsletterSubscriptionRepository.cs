using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;

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
    ValueTask<List<(int NewsId, int UserId)>> GetDueNewsScheduleForSlotAsync(
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
        int newsId,
        int userId,
        TimeZoneInfo newsletterTimeZone,
        DateTime referenceUtcExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a newsletter subscription by ID for a specific user.
    /// </summary>
    ValueTask<NewsletterSubscription?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a newsletter subscription by its ID.
    /// </summary>
    ValueTask<NewsletterSubscription?> FindNewsByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all newsletter subscriptions belonging to a user.
    /// </summary>
    ValueTask<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);
}
