using Hermes.Domain.Entities;
using Hermes.Domain.Interfaces.DBContext;

namespace Hermes.Application.Services;

public sealed class NotificationLogService(IHermesDbContext db) : INotificationLogService
{
    public async Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await db.SetNotificationLogAsync(log, cancellationToken).ConfigureAwait(false);
    }
}
