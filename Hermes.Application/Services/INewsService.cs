using Hermes.Application.Models.News;
using Hermes.Domain.Entities;

namespace Hermes.Application.Services;

public interface INewsService
{
    Task<int> SetNewsAsync(News news, CancellationToken cancellationToken = default);

    Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default);

    Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default);

    Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default);

    Task<NewsListResult> GetNewsListAsync(NewsListQuery query, CancellationToken cancellationToken = default);

    Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);
}
