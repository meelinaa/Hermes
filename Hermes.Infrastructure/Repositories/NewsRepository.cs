using Hermes.Domain.Entities;
using Hermes.Infrastructure.Data.Interfaces;
using Hermes.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="INewsRepository"/>.
/// </summary>
public sealed class NewsRepository : INewsRepository
{
    private readonly IHermesDbContext _db;

    /// <summary>
    /// Initializes a new instance of <see cref="NewsRepository"/>.
    /// </summary>
    public NewsRepository(IHermesDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<News?> GetNewsByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return await _db.News.AsNoTracking().FirstOrDefaultAsync(n => n.UserId == userId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CreateNewsAsync(News news, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        await _db.News.AddAsync(news, ct).ConfigureAwait(false);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateNewsAsync(News news, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        _db.News.Update(news);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteNewsAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.News.FirstOrDefaultAsync(n => n.Id == id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        _db.News.Remove(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
