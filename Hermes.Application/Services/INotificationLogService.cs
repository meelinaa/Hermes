using Hermes.Domain.Entities;

namespace Hermes.Application.Services;

public interface INotificationLogService
{
    Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
}
