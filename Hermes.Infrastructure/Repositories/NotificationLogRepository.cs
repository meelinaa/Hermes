using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Interfaces.Repositories;
using Hermes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="INotificationLogRepository"/>.
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="NotificationLogRepository"/>.
/// </remarks>
public sealed class NotificationLogRepository(HermesDbContext db) : INotificationLogRepository
{

    /// <inheritdoc />
    public async Task CreateAsync(NotificationLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await db.NotificationLogs.AddAsync(log, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(NotificationLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        db.NotificationLogs.Update(log);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<NotificationLog>> GetPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await db.NotificationLogs
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
