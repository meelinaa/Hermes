using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

public interface INotificationLogRepository
{
    ValueTask SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
    ValueTask<NotificationLog?> GetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
    ValueTask<bool> ExistsSentNotificationInWindowAsync(UserId userId, NewsletterId newsId, DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken cancellationToken = default);
}
