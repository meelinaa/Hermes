using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Validators;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Hermes.Infrastructure.Adapters.Outbound.Repositories;

/// <summary>
/// Infrastructure adapter for persisting, updating, and atomically reserving notification audit logs.
/// </summary>
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
    public async ValueTask UpdateNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (log.Id <= 0)
            throw new ArgumentException("NotificationLog id must be greater than zero for updates.", nameof(log));

        db.NotificationLogs.Update(log);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<SlotReservationResult> TryReserveSlotAsync(
        NotificationLog log,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (log.Id != 0)
            throw new ArgumentException("New reservation must have id 0.", nameof(log));

        var existsResult = await UserExistenceValidator.EnsureExistsAsync(db, log.UserId, cancellationToken).ConfigureAwait(false);
        if (existsResult.IsFailed)
            throw new UserNotFoundException(existsResult.Errors[0].Message);

        DateTime nowUtc = DateTime.UtcNow;
        DateTime staleThresholdUtc = nowUtc - leaseDuration;

        // In-memory provider emulation for unique constraint
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            NotificationLog? inMemoryExisting = await db.NotificationLogs
                .FirstOrDefaultAsync(
                    n => n.UserId == log.UserId
                         && n.NewsId == log.NewsId
                         && n.Channel == log.Channel
                         && n.ScheduledSlotUtc == log.ScheduledSlotUtc,
                    cancellationToken)
                .ConfigureAwait(false);

            if (inMemoryExisting is not null)
            {
                if (inMemoryExisting.Status == NotificationStatus.Sent)
                    return SlotReservationResult.AlreadySent();

                if (inMemoryExisting.Status == NotificationStatus.Pending)
                {
                    if (inMemoryExisting.CreatedAtUtc > staleThresholdUtc)
                        return SlotReservationResult.ActiveLease();

                    inMemoryExisting.ReclaimLease(nowUtc);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return SlotReservationResult.Reclaimed(inMemoryExisting);
                }

                return SlotReservationResult.AlreadySent();
            }

            await db.NotificationLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return SlotReservationResult.NewReservation(log);
        }

        try
        {
            await db.NotificationLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return SlotReservationResult.NewReservation(log);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Detach failed entity to keep DbContext clean
            db.Entry(log).State = EntityState.Detached;

            NotificationLog? existing = await db.NotificationLogs
                .FirstOrDefaultAsync(
                    n => n.UserId == log.UserId
                         && n.NewsId == log.NewsId
                         && n.Channel == log.Channel
                         && n.ScheduledSlotUtc == log.ScheduledSlotUtc,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
                return SlotReservationResult.ActiveLease();

            if (existing.Status == NotificationStatus.Sent)
                return SlotReservationResult.AlreadySent();

            if (existing.Status == NotificationStatus.Pending)
            {
                if (existing.CreatedAtUtc > staleThresholdUtc)
                {
                    // In-flight active delivery (< 60s); do not steal lease
                    return SlotReservationResult.ActiveLease();
                }

                // Stale pending (> 60s); attempt atomic Compare-And-Swap (CAS) update
                int affectedRows = await db.NotificationLogs
                    .Where(n => n.Id == existing.Id && n.Status == NotificationStatus.Pending && n.CreatedAtUtc <= staleThresholdUtc)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(n => n.CreatedAtUtc, nowUtc)
                        .SetProperty(n => n.RetryCount, n => n.RetryCount + 1),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (affectedRows == 1)
                {
                    existing.ReclaimLease(nowUtc);
                    return SlotReservationResult.Reclaimed(existing);
                }

                // Lost CAS race to another worker
                return SlotReservationResult.ActiveLease();
            }

            return SlotReservationResult.AlreadySent();
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> ReapStalePendingNotificationsAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        DateTime cutoffUtc = DateTime.UtcNow - olderThan;

        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var staleLogs = await db.NotificationLogs
                .Where(n => n.Status == NotificationStatus.Pending && n.CreatedAtUtc <= cutoffUtc)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var log in staleLogs)
            {
                log.MarkAsFailed("Worker lease expired before completion", null);
            }

            if (staleLogs.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return staleLogs.Count;
        }

        return await db.NotificationLogs
            .Where(n => n.Status == NotificationStatus.Pending && n.CreatedAtUtc <= cutoffUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.Status, NotificationStatus.Failed)
                .SetProperty(n => n.ErrorMessage, "Worker lease expired before completion"),
                cancellationToken)
            .ConfigureAwait(false);
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
                                   && ((notificationLog.ScheduledSlotUtc >= windowStartUtc && notificationLog.ScheduledSlotUtc < windowEndUtc)
                                       || (notificationLog.SentAt >= windowStartUtc && notificationLog.SentAt < windowEndUtc)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Evaluates if an exception represents a database unique constraint / duplicate key violation.
    /// </summary>
    /// <param name="ex">The DbUpdateException to test.</param>
    /// <returns>True if the error is a duplicate entry violation; otherwise false.</returns>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is MySqlException mySqlEx && mySqlEx.Number == 1062)
            return true;

        string msg = ex.ToString();
        return msg.Contains("1062")
               || msg.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("UX_notification_logs_slot_reservation", StringComparison.OrdinalIgnoreCase);
    }
}
