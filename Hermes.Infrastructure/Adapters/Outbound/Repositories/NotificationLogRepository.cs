using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Hermes.Domain.Exceptions;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Validators;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Adapters.Outbound.Repositories;

/// <inheritdoc />
public sealed class NotificationLogRepository(HermesDbContext db) : INotificationLogRepository
{
    /// <inheritdoc />
    public async ValueTask SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (log.Id != 0)
            throw new ArgumentException("New notification logs must have id 0 before insert.", nameof(log));

        var existsResult = await UserExistenceValidator.EnsureExistsAsync(db, log.UserId, cancellationToken).ConfigureAwait(false);
        if (existsResult.IsFailed)
            throw new UserNotFoundException(existsResult.Errors[0].Message);
        await db.NotificationLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<NotificationLog?> GetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (log.Id <= 0)
            return null;

        return await db.NotificationLogs.AsNoTracking()
            .FirstOrDefaultAsync(notificationLog => notificationLog.Id == log.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> ExistsSentNotificationInWindowAsync(
        UserId userId,
        NewsletterId newsId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        return await db.NotificationLogs.AsNoTracking()
            .AnyAsync(
                notificationLog => notificationLog.UserId == userId
                                   && notificationLog.NewsId == newsId
                                   && notificationLog.Channel == DeliveryChannel.Email
                                   && notificationLog.Status == NotificationStatus.Sent
                                   && notificationLog.SentAt >= windowStartUtc
                                   && notificationLog.SentAt < windowEndUtc,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
