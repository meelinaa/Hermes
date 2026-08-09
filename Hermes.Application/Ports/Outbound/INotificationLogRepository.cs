using Hermes.Domain.Entities;

namespace Hermes.Application.Ports.Outbound;

public interface INotificationLogRepository
{
    ValueTask SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
    ValueTask<NotificationLog?> GetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
    ValueTask<bool> ExistsSentNotificationInWindowAsync(int userId, int newsId, DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken cancellationToken = default);
}
