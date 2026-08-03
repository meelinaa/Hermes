using Hermes.Domain.Entities;

namespace Hermes.Application.Ports.Inbound;

public interface INotificationLogService
{
    Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
}
