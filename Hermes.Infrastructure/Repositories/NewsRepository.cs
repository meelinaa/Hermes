using Hermes.Domain.Entities;
using Hermes.Domain.Interfaces.DBContext;
using Hermes.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="INewsRepository"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="NewsRepository"/>.
/// </remarks>
public sealed class NewsRepository(IHermesDbContext db) : INewsRepository
{

    /// <inheritdoc />
    public async Task<News?> GetNewsByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return await db.News.AsNoTracking().FirstOrDefaultAsync(n => n.UserId == userId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CreateNewsAsync(News news, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        await db.News.AddAsync(news, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateNewsAsync(News news, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        db.News.Update(news);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteNewsAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.News.FirstOrDefaultAsync(n => n.Id == id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        db.News.Remove(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
