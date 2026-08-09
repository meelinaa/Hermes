using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services.Newsletter;

/// <summary>
/// Service implementation for managing newsletter subscription domain entities, schedule window assignments, and persistence operations.
/// </summary>
public sealed class NewsletterSubscriptionService(
    INewsletterSubscriptionRepository db,
    IOptions<NewsletterOptions> newsletterOptions) : INewsletterSubscriptionService
{
    /// <summary>
    /// Validates schedule window requirements and persists a new newsletter subscription profile.
    /// </summary>
    /// <param name="news">The subscription entity to create and persist.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The unique database identifier assigned to the created subscription.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="news"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the owning user ID is less than or equal to zero.</exception>
    public async ValueTask<int> SetNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if (news.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(news.UserId), "Owning user ID must be greater than zero.");
        ScheduleWindow window = ScheduleWindow.EnsureForDigestScheduling(news.SendOnWeekdays, news.SendAtTimes);
        news.AssignDigestSchedule(window);
        await db.SetNewsAsync(news, cancellationToken).ConfigureAwait(false);
        await AdvanceDigestSlotAfterMutationAsync(news, cancellationToken).ConfigureAwait(false);
        return news.Id;
    }

    /// <summary>
    /// Updates an existing newsletter subscription's settings, validating updated schedule windows and recalculating its next run slot.
    /// </summary>
    /// <param name="news">The updated subscription entity to persist.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="news"/> is <c>null</c>.</exception>
    public async ValueTask UpdateNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        ScheduleWindow window = ScheduleWindow.EnsureForDigestScheduling(news.SendOnWeekdays, news.SendAtTimes);
        news.AssignDigestSchedule(window);
        await db.UpdateNewsAsync(news, cancellationToken).ConfigureAwait(false);
        await AdvanceDigestSlotAfterMutationAsync(news, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves configured timezone settings and advances the next digest run slot for a mutated subscription profile.
    /// </summary>
    /// <param name="news">The subscription profile containing ID and user information.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    private async ValueTask AdvanceDigestSlotAfterMutationAsync(NewsletterSubscription news, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone = NewsletterSchedulingProvider.ResolveTimeZone(newsletterOptions.Value.TimeZoneId);
        await db.AdvanceNextDigestSlotAsync(news.Id, news.UserId, zone, DateTime.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a specific newsletter subscription from the persistence store.
    /// </summary>
    /// <param name="news">The subscription entity to remove.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="news"/> is <c>null</c>.</exception>
    public async ValueTask DeleteNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        await db.DeleteNewsAsync(news, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a newsletter subscription by ID for a verified owning user.
    /// </summary>
    /// <param name="userId">The unique identifier of the owning user.</param>
    /// <param name="id">The unique identifier of the target newsletter subscription.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="NewsletterSubscription"/> if found; otherwise <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> or <paramref name="id"/> is less than or equal to zero.</exception>
    public async ValueTask<NewsletterSubscription?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        if (id <= 0)
            throw new ArgumentException("News id must be greater than zero.", nameof(id));
        return await db.GetNewsByIdAsync(userId, id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds a newsletter subscription by its database identifier regardless of owning user context.
    /// </summary>
    /// <param name="id">The unique identifier of the target newsletter subscription.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The matching <see cref="NewsletterSubscription"/> if found; otherwise <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is less than or equal to zero.</exception>
    public async ValueTask<NewsletterSubscription?> FindNewsByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("News id must be greater than zero.", nameof(id));
        return await db.FindNewsByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Queries a paged collection of newsletter subscriptions belonging to a user, with optional filtering and cursor pagination.
    /// </summary>
    /// <param name="query">The query parameter DTO containing filter options, sorting preference, and pagination boundaries.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="NewsletterSubscriptionListResultDto"/> containing matching items and pagination metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when query parameter constraints (such as positive user ID or valid cursor pagination) are violated.</exception>
    public async ValueTask<NewsletterSubscriptionListResultDto> GetNewsListAsync(NewsletterSubscriptionListQueryDto query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.UserId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(query));
        if (query.Page < 1)
            throw new ArgumentException("Page must be at least 1.", nameof(query));
        if (query.PageSize < 1)
            throw new ArgumentException("Page size must be at least 1.", nameof(query));
        if (query.AfterId is not null && query.SortDescending)
        {
            throw new ArgumentException(
                "Cursor pagination (afterId) is only supported with ascending id order (sort=id or omit sort).",
                nameof(query));
        }

        return await db.GetNewsListAsync(query, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes all newsletter subscriptions associated with a specific user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user whose subscriptions should be purged.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The total number of subscription records deleted.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> is less than or equal to zero.</exception>
    public async ValueTask<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        return await db.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
