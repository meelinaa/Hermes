using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Infrastructure.Data.Interfaces;
using Hermes.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="INotificationLogRepository"/>.
/// </summary>
public sealed class NotificationLogRepository : INotificationLogRepository
{
    private readonly IHermesDbContext _db;

    /// <summary>
    /// Initializes a new instance of <see cref="NotificationLogRepository"/>.
    /// </summary>
    public NotificationLogRepository(IHermesDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task CreateAsync(NotificationLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await _db.NotificationLogs.AddAsync(log, ct).ConfigureAwait(false);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(NotificationLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        _db.NotificationLogs.Update(log);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NotificationLog>> GetPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.NotificationLogs
            .AsNoTracking()
            .Where(l =>
                l.Status == NotificationStatus.Pending
                || (l.Status == NotificationStatus.Failed && l.NextRetryAt != null && l.NextRetryAt <= now))
            .OrderBy(l => l.NextRetryAt ?? DateTime.MaxValue)
            .ThenBy(l => l.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
