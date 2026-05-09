using Hermes.Domain.Entities;
using Hermes.Application.Ports;

namespace Hermes.Application.Services;

public sealed class NewsService(IHermesDataStore db) : INewsService
{
    /// <summary>Creates a news entry and returns its persisted identifier.</summary>
    public async Task<int> SetNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if(news.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(news.UserId), "Owning user ID must be greater than zero.");
        await db.SetNewsAsync(news, cancellationToken).ConfigureAwait(false);
        return news.Id;
    }

    /// <summary>Updates an existing news entry.</summary>
    public async Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        await db.UpdateNewsAsync(news, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Returns all news entries for the specified user.</summary>
    public async Task<List<News>> GetAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        return await db.GetAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes all news entries for the specified user and returns the deleted row count.</summary>
    public async Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(userId));
        return await db.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
    }
}
