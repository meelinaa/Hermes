using Hermes.Domain.Entities;

namespace Hermes.Infrastructure.Repositories.Interfaces;

public interface INewsRepository
{
    Task<News?> GetNewsByUserIdAsync(int userId, CancellationToken ct = default);
    Task CreateNewsAsync(News news, CancellationToken ct = default);
    Task UpdateNewsAsync(News news, CancellationToken ct = default);
    Task DeleteNewsAsync(int id, CancellationToken ct = default);
}
