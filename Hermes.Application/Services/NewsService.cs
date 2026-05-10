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

    public async Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        await db.DeleteNewsAsync(news, cancellationToken).ConfigureAwait(false);
    }

    public async Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        if (id <= 0)
            throw new ArgumentException("News id must be greater than zero.", nameof(id));
        return await db.GetNewsByIdAsync(userId, id, cancellationToken).ConfigureAwait(false);
    }

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

    public async Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        return await db.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
