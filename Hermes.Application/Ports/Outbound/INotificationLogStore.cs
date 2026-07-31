using Hermes.Domain.Entities;

namespace Hermes.Application.Ports.Outbound;

public interface INotificationLogStore
{
    Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
    Task<NotificationLog?> GetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
    Task<bool> ExistsSentNotificationInWindowAsync(int userId, int newsId, DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken cancellationToken = default);
}
