using Hermes.Application.Models.News;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Scheduling;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services;

public sealed class NewsService(INewsStore db, IOptions<NewsletterOptions> newsletterOptions) : INewsService
{
    /// <summary>Creates a news entry and returns its persisted identifier.</summary>
    public async Task<int> SetNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if(news.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(news.UserId), "Owning user ID must be greater than zero.");
        ScheduleWindow window = ScheduleWindow.EnsureForDigestScheduling(news.SendOnWeekdays, news.SendAtTimes);
        news.AssignDigestSchedule(window);
        await db.SetNewsAsync(news, cancellationToken).ConfigureAwait(false);
        await AdvanceDigestSlotAfterMutationAsync(news, cancellationToken).ConfigureAwait(false);
        return news.Id;
    }

    /// <summary>Updates an existing news entry.</summary>
    public async Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        ScheduleWindow window = ScheduleWindow.EnsureForDigestScheduling(news.SendOnWeekdays, news.SendAtTimes);
        news.AssignDigestSchedule(window);
        await db.UpdateNewsAsync(news, cancellationToken).ConfigureAwait(false);
        await AdvanceDigestSlotAfterMutationAsync(news, cancellationToken).ConfigureAwait(false);
    }

    private async Task AdvanceDigestSlotAfterMutationAsync(News news, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone = NewsletterSchedulingClock.ResolveTimeZone(newsletterOptions.Value.TimeZoneId);
        await db.AdvanceNextDigestSlotAsync(news.Id, news.UserId, zone, DateTime.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Deletes a news entry.</summary>
    public async Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        await db.DeleteNewsAsync(news, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns a single news entry by user and news identifiers.</summary>
    public async Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        if (id <= 0)
            throw new ArgumentException("News id must be greater than zero.", nameof(id));
        return await db.GetNewsByIdAsync(userId, id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns one page of news for the user (offset and/or cursor), with optional filter and sort.</summary>
    public async Task<NewsListResult> GetNewsListAsync(NewsListQuery query, CancellationToken cancellationToken = default)
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

    /// <summary>Deletes all news entries for the specified user and returns the deleted row count.</summary>
    public async Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        return await db.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
