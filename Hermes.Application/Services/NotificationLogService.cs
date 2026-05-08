using Hermes.Domain.Entities;
using Hermes.Application.Ports;

namespace Hermes.Application.Services;

public sealed class NotificationLogService(IHermesDataStore db) : INotificationLogService
{
    /// <summary>Persists a notification log entry.</summary>
    public async Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await db.SetNotificationLogAsync(log, cancellationToken).ConfigureAwait(false);
    }
}
