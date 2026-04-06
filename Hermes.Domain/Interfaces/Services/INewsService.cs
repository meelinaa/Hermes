using Hermes.Domain.Entities;

namespace Hermes.Application.Services;

public interface INewsService
{
    Task SetNewsAsync(News news, CancellationToken cancellationToken = default);
    Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default);
    Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default);
    Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default);
    Task<List<News>> GetAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes all news rows for the user; returns how many were removed.</summary>
    Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);
}
