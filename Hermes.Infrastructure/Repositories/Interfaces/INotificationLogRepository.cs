using Hermes.Domain.Entities;

namespace Hermes.Infrastructure.Repositories.Interfaces;

public interface INotificationLogRepository
{
    Task CreateAsync(NotificationLog log, CancellationToken ct = default);
    Task UpdateAsync(NotificationLog log, CancellationToken ct = default);
    Task<IEnumerable<NotificationLog>> GetPendingAsync(CancellationToken ct = default);
}
