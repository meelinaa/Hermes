using Hermes.Application.Models.NewsletterSubscription;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Scheduling;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services;

/// <summary>
/// Service implementation for managing newsletter subscriptions.
/// </summary>
public sealed class NewsletterSubscriptionService(INewsletterSubscriptionStore db, IOptions<NewsletterOptions> newsletterOptions) : INewsletterSubscriptionService
{
    /// <summary>
    /// Creates or sets a newsletter subscription, validating the schedule and advancing its next run time.
    /// </summary>
    public async Task<int> SetNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
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
    /// Updates an existing newsletter subscription's config, schedule, and next run time.
    /// </summary>
    public async Task UpdateNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        ScheduleWindow window = ScheduleWindow.EnsureForDigestScheduling(news.SendOnWeekdays, news.SendAtTimes);
        news.AssignDigestSchedule(window);
        await db.UpdateNewsAsync(news, cancellationToken).ConfigureAwait(false);
        await AdvanceDigestSlotAfterMutationAsync(news, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Recalculates and advances the next digest run slot for a mutated newsletter subscription.
    /// </summary>
    private async Task AdvanceDigestSlotAfterMutationAsync(NewsletterSubscription news, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone = NewsletterSchedulingClock.ResolveTimeZone(newsletterOptions.Value.TimeZoneId);
        await db.AdvanceNextDigestSlotAsync(news.Id, news.UserId, zone, DateTime.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a specific newsletter subscription from the store.
    /// </summary>
    public async Task DeleteNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        await db.DeleteNewsAsync(news, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a newsletter subscription by ID for a specific user.
    /// </summary>
    public async Task<NewsletterSubscription?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        if (id <= 0)
            throw new ArgumentException("News id must be greater than zero.", nameof(id));
        return await db.GetNewsByIdAsync(userId, id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds a newsletter subscription by its ID.
    /// </summary>
    public async Task<NewsletterSubscription?> FindNewsByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("News id must be greater than zero.", nameof(id));
        return await db.FindNewsByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a paged list of newsletter subscriptions matching the query parameters.
    /// </summary>
    public async Task<NewsletterSubscriptionListResult> GetNewsListAsync(NewsletterSubscriptionListQuery query, CancellationToken cancellationToken = default)
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
    /// Deletes all newsletter subscriptions belonging to a user.
    /// </summary>
    public async Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        return await db.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
