using Hermes.Application.Models.News;
using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;

namespace Hermes.Application.Ports;

/// <summary>News and newsletter-schedule persistence.</summary>
public interface INewsStore
{
    Task SetNewsAsync(News news, CancellationToken cancellationToken = default);
    Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default);
    Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default);
    Task<NewsListResult> GetNewsListAsync(NewsListQuery query, CancellationToken cancellationToken = default);
    Task<List<NewsScheduleRow>> GetNewsScheduleRowsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns newsletter profiles that are due for the given local weekday and clock slot, evaluated in MySQL
    /// (JSON on <c>news</c> columns) so schedulers do not load all rows every tick.
    /// Requires MySQL 8+ for <c>JSON_SEARCH</c> / <c>JSON_VALID</c>.
    /// </summary>
    Task<List<(int NewsId, int UserId)>> GetDueNewsScheduleForSlotAsync(Weekdays weekday, int hour, int minute, CancellationToken cancellationToken = default);

    Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default);
    Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);
}
