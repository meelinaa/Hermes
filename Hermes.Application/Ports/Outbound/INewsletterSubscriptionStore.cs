using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Outbound port for managing newsletter subscription persistence, retrieval, and pagination.
/// </summary>
public interface INewsletterSubscriptionStore
{
    /// <summary>
    /// Persists a new newsletter subscription in the store.
    /// </summary>
    /// <param name="news">The newsletter subscription entity to persist.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask SetNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing newsletter subscription in the store.
    /// </summary>
    /// <param name="news">The newsletter subscription entity with modified state.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask UpdateNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the specified newsletter subscription from the store.
    /// </summary>
    /// <param name="news">The newsletter subscription entity to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask DeleteNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paged list of newsletter subscriptions matching the query parameters.
    /// </summary>
    /// <param name="query">The query and pagination parameters.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A DTO containing items and pagination metadata.</returns>
    ValueTask<NewsletterSubscriptionListResultDto> GetNewsListAsync(NewsletterSubscriptionListQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a newsletter subscription by ID for a specific user.
    /// </summary>
    /// <param name="userId">The owner user ID.</param>
    /// <param name="id">The newsletter subscription ID.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The newsletter subscription or null if not found.</returns>
    ValueTask<NewsletterSubscription?> GetNewsByIdAsync(UserId userId, NewsletterId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a newsletter subscription by its unique ID.
    /// </summary>
    /// <param name="id">The newsletter subscription ID.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The newsletter subscription or null if not found.</returns>
    ValueTask<NewsletterSubscription?> FindNewsByIdAsync(NewsletterId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all newsletter subscriptions belonging to a specified user.
    /// </summary>
    /// <param name="userId">The owner user ID.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The count of deleted subscriptions.</returns>
    ValueTask<int> DeleteAllNewsByUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
