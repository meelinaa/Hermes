using Hermes.Domain.Entities;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;

namespace Hermes.Application.Services;

public sealed class NotificationLogService(INotificationLogStore db) : INotificationLogService
{
    public async Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await db.SetNotificationLogAsync(log, cancellationToken).ConfigureAwait(false);
    }
}
