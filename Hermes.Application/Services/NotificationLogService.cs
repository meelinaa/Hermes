using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;

namespace Hermes.Application.Services;

/// <summary>
/// Service implementation for persisting notification audit logs to record email delivery attempts and status outcomes.
/// </summary>
public sealed class NotificationLogService(INotificationLogRepository db) : INotificationLogService
{
    /// <summary>
    /// Persists a notification delivery log entry to track successful or failed email dispatch attempts.
    /// </summary>
    /// <param name="log">The notification log entity describing the user, news item, delivery status, and potential error message.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    public async Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await db.SetNotificationLogAsync(log, cancellationToken).ConfigureAwait(false);
    }
}
