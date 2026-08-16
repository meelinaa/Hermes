using Hermes.Domain.Entities;

namespace Hermes.Application.Ports.Inbound;

public interface INotificationLogService
{
    ValueTask SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
}
