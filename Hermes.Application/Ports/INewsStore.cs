using Hermes.Application.Models.News;using Hermes.Domain.Entities;
using Hermes.Domain.Enums;

namespace Hermes.Application.Ports;

/// <summary>News and newsletter-schedule persistence.</summary>
public interface INewsStore
{
    Task SetNewsAsync(News news, CancellationToken cancellationToken = default);
    Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default);
    Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default);
    Task<NewsListResult> GetNewsListAsync(NewsListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns newsletter rows due in the one-minute window [<paramref name="slotStartUtc"/>, <paramref name="slotEndUtc"/>).
    /// Uses <see cref="News.NextDigestSlotUtc"/> when set; otherwise falls back to JSON matching (MySQL 8+).
    /// </summary>
    Task<List<(int NewsId, int UserId)>> GetDueNewsScheduleForSlotAsync(
        Weekdays weekday,
        int hour,
        int minute,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Recomputes and persists <see cref="News.NextDigestSlotUtc"/> after a slot attempt or schedule change.</summary>
    Task AdvanceNextDigestSlotAsync(
        int newsId,
        int userId,
        TimeZoneInfo newsletterTimeZone,
        DateTime referenceUtcExclusive,
        CancellationToken cancellationToken = default);

    Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default);
    Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);
}
