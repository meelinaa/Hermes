using Hermes.Domain.Entities;
using Hermes.Application.Ports;

namespace Hermes.Application.Services;

public sealed class NewsService(IHermesDataStore db) : INewsService
{
    public async Task<int> SetNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if(news.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(news.UserId), "Owning user ID must be greater than zero.");
        await db.SetNewsAsync(news, cancellationToken).ConfigureAwait(false);
        return news.Id;
    }

    public async Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        await db.UpdateNewsAsync(news, cancellationToken).ConfigureAwait(false);
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

    public async Task<List<News>> GetAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        return await db.GetAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        return await db.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
