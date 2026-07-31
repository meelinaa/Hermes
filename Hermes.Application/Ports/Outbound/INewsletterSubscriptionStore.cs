using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Defines the data access port for managing newsletter subscriptions.
/// </summary>
public interface INewsletterSubscriptionStore
{
    /// <summary>
    /// Persists a new newsletter subscription in the store.
    /// </summary>
    Task SetNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing newsletter subscription in the store.
    /// </summary>
    Task UpdateNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified newsletter subscription from the store.
    /// </summary>
    Task DeleteNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paged list of newsletter subscriptions matching the query parameters.
    /// </summary>
    Task<NewsletterSubscriptionListResult> GetNewsListAsync(NewsletterSubscriptionListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves newsletter subscription schedules that are due for delivery in the specified slot.
    /// </summary>
    Task<List<(int NewsId, int UserId)>> GetDueNewsScheduleForSlotAsync(
        Weekdays weekday,
        int hour,
        int minute,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the next digest slot timestamp for a newsletter subscription.
    /// </summary>
    Task AdvanceNextDigestSlotAsync(
        int newsId,
        int userId,
        TimeZoneInfo newsletterTimeZone,
        DateTime referenceUtcExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a newsletter subscription by ID for a specific user.
    /// </summary>
    Task<NewsletterSubscription?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a newsletter subscription by its ID.
    /// </summary>
    Task<NewsletterSubscription?> FindNewsByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all newsletter subscriptions belonging to a user.
    /// </summary>
    Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);
}
