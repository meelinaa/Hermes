using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;

namespace Hermes.Application.Services;

public sealed class NotificationLogService(INotificationLogRepository db) : INotificationLogService
{
    public async Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await db.SetNotificationLogAsync(log, cancellationToken).ConfigureAwait(false);
    }
}
