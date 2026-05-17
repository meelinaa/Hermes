using Hermes.Application.Models.News;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;

namespace Hermes.Application.Ports;

public interface INewsStore
{
    Task SetNewsAsync(News news, CancellationToken cancellationToken = default);
    Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default);
    Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default);
    Task<NewsListResult> GetNewsListAsync(NewsListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Due rows in [slotStartUtc,slotEndUtc); uses <see cref="News.NextDigestSlotUtc"/> else JSON (MySQL).</summary>
    Task<List<(int NewsId, int UserId)>> GetDueNewsScheduleForSlotAsync(
        Weekdays weekday,
        int hour,
        int minute,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Rebuild <see cref="News.NextDigestSlotUtc"/> after send attempt or schedule edit.</summary>
    Task AdvanceNextDigestSlotAsync(
        int newsId,
        int userId,
        TimeZoneInfo newsletterTimeZone,
        DateTime referenceUtcExclusive,
        CancellationToken cancellationToken = default);

    Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default);

    Task<News?> FindNewsByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);
}
