using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Domain.Entities;

namespace Hermes.Application.Ports.Inbound;

/// <summary>
/// Service interface for managing newsletter subscriptions in the application layer.
/// </summary>
public interface INewsletterSubscriptionService
{
    /// <summary>
    /// Creates or sets a newsletter subscription.
    /// </summary>
    Task<int> SetNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing newsletter subscription.
    /// </summary>
    Task UpdateNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific newsletter subscription.
    /// </summary>
    Task DeleteNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a newsletter subscription by ID for a specific user.
    /// </summary>
    Task<NewsletterSubscription?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a newsletter subscription by its ID.
    /// </summary>
    Task<NewsletterSubscription?> FindNewsByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paged list of newsletter subscriptions matching the query criteria.
    /// </summary>
    Task<NewsletterSubscriptionListResultDto> GetNewsListAsync(NewsletterSubscriptionListQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all newsletter subscriptions belonging to a user.
    /// </summary>
    Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);
}
