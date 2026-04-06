using Hermes.Domain.Entities;

namespace Hermes.Domain.Interfaces.Repositories;

public interface INewsRepository
{
    /// <summary>Returns all news rows for the given user (may be empty).</summary>
    Task<IReadOnlyList<News>> GetNewsByUserIdAsync(int userId, CancellationToken ct = default);
    Task CreateNewsAsync(News news, CancellationToken ct = default);
    Task UpdateNewsAsync(News news, CancellationToken ct = default);
    Task DeleteNewsAsync(int id, CancellationToken ct = default);
}
