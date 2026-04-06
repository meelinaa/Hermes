using Hermes.Domain.Entities;

namespace Hermes.Application.Services;

/// <summary>Application operations for <see cref="NotificationLog"/> persistence.</summary>
public interface INotificationLogService
{
    Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
}
