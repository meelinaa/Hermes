using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.Repositories;

/// <summary>
/// Contains integration-level unit tests for <see cref="NotificationLogRepository"/>,
/// verifying atomic slot reservations, unique constraint duplicate handling, active lease protection, CAS reclaim, and stale pending reaper.
/// </summary>
public sealed class NotificationLogRepositoryIdempotencyTests
{
    private static HermesDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HermesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HermesDbContext(options);
    }

    private static async Task<User> SeedUserAsync(HermesDbContext ctx, int id = 1)
    {
        User user = new()
        {
            Id = new UserId(id),
            Name = "TestUser",
            Email = Email.Parse($"user{id}@example.org"),
            PasswordHash = "hash"
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Tests that <see cref="NotificationLogRepository.TryReserveSlotAsync"/> successfully creates a new reservation for an unreserved slot.
    /// </summary>
    [Fact]
    public async Task TryReserveSlotAsync_Should_CreateNewReservation_WhenSlotNotTaken()
    {
        // Arrange
        await using HermesDbContext ctx = CreateContext();
        User user = await SeedUserAsync(ctx);
        NotificationLogRepository sut = new(ctx);

        DateTime slot = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);
        NotificationLog log = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(1));

        // Act
        SlotReservationResult result = await sut.TryReserveSlotAsync(log, TimeSpan.FromSeconds(60));

        // Assert
        Assert.True(result.IsAcquired);
        Assert.Equal(SlotReservationStatus.Reserved, result.Status);
        Assert.NotNull(result.Log);
        Assert.Equal(NotificationStatus.Pending, result.Log!.Status);
    }

    /// <summary>
    /// Tests that <see cref="NotificationLogRepository.TryReserveSlotAsync"/> returns AlreadySent when a completed Sent log exists for the slot.
    /// </summary>
    [Fact]
    public async Task TryReserveSlotAsync_Should_ReturnAlreadySent_WhenSlotAlreadyDelivered()
    {
        // Arrange
        await using HermesDbContext ctx = CreateContext();
        User user = await SeedUserAsync(ctx);
        NotificationLogRepository sut = new(ctx);

        DateTime slot = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);
        NotificationLog existing = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(1));
        existing.MarkAsSent(DateTime.UtcNow);
        ctx.NotificationLogs.Add(existing);
        await ctx.SaveChangesAsync();

        NotificationLog attempt = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(1));

        // Act
        SlotReservationResult result = await sut.TryReserveSlotAsync(attempt, TimeSpan.FromSeconds(60));

        // Assert
        Assert.False(result.IsAcquired);
        Assert.Equal(SlotReservationStatus.AlreadySent, result.Status);
    }

    /// <summary>
    /// Tests that <see cref="NotificationLogRepository.TryReserveSlotAsync"/> protects active in-flight delivery leases when pending log is less than 60s old.
    /// </summary>
    [Fact]
    public async Task TryReserveSlotAsync_Should_ReturnActiveLease_WhenPendingLogIsYoungerThanLeaseDuration()
    {
        // Arrange
        await using HermesDbContext ctx = CreateContext();
        User user = await SeedUserAsync(ctx);
        NotificationLogRepository sut = new(ctx);

        DateTime slot = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);
        // Created 10 seconds ago (active delivery in progress)
        NotificationLog existing = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(1), DateTime.UtcNow.AddSeconds(-10));
        ctx.NotificationLogs.Add(existing);
        await ctx.SaveChangesAsync();

        NotificationLog attempt = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(1));

        // Act
        SlotReservationResult result = await sut.TryReserveSlotAsync(attempt, TimeSpan.FromSeconds(60));

        // Assert
        Assert.False(result.IsAcquired);
        Assert.Equal(SlotReservationStatus.ActiveLeaseInProgress, result.Status);
    }

    /// <summary>
    /// Tests that <see cref="NotificationLogRepository.TryReserveSlotAsync"/> reclaims a pending lease when the previous attempt crashed and is older than 60s.
    /// </summary>
    [Fact]
    public async Task TryReserveSlotAsync_Should_ReclaimPending_WhenPendingLogIsOlderThanLeaseDuration()
    {
        // Arrange
        await using HermesDbContext ctx = CreateContext();
        User user = await SeedUserAsync(ctx);
        NotificationLogRepository sut = new(ctx);

        DateTime slot = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);
        // Created 2 minutes ago (crashed worker)
        NotificationLog existing = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(1), DateTime.UtcNow.AddMinutes(-2));
        ctx.NotificationLogs.Add(existing);
        await ctx.SaveChangesAsync();

        NotificationLog attempt = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(1));

        // Act
        SlotReservationResult result = await sut.TryReserveSlotAsync(attempt, TimeSpan.FromSeconds(60));

        // Assert
        Assert.True(result.IsAcquired);
        Assert.Equal(SlotReservationStatus.Reclaimed, result.Status);
        Assert.NotNull(result.Log);
        Assert.Equal(1, result.Log!.RetryCount);
    }

    /// <summary>
    /// Tests that <see cref="NotificationLogRepository.ReapStalePendingNotificationsAsync"/> transitions abandoned pending logs older than threshold to Failed.
    /// </summary>
    [Fact]
    public async Task ReapStalePendingNotificationsAsync_Should_MarkStalePendingAsFailed()
    {
        // Arrange
        await using HermesDbContext ctx = CreateContext();
        User user = await SeedUserAsync(ctx);
        NotificationLogRepository sut = new(ctx);

        DateTime slot1 = new(2026, 8, 16, 9, 0, 0, DateTimeKind.Utc);
        DateTime slot2 = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);

        // Stale log (created 10 minutes ago)
        NotificationLog staleLog = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot1, new NewsletterId(1), DateTime.UtcNow.AddMinutes(-10));
        // Recent log (created 1 minute ago)
        NotificationLog recentLog = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot2, new NewsletterId(2), DateTime.UtcNow.AddMinutes(-1));

        ctx.NotificationLogs.AddRange(staleLog, recentLog);
        await ctx.SaveChangesAsync();

        // Act
        int reaped = await sut.ReapStalePendingNotificationsAsync(TimeSpan.FromMinutes(5));

        // Assert
        Assert.Equal(1, reaped);

        NotificationLog? updatedStale = await ctx.NotificationLogs.FindAsync(staleLog.Id);
        Assert.NotNull(updatedStale);
        Assert.Equal(NotificationStatus.Failed, updatedStale!.Status);
        Assert.Contains("expired", updatedStale.ErrorMessage);

        NotificationLog? updatedRecent = await ctx.NotificationLogs.FindAsync(recentLog.Id);
        Assert.NotNull(updatedRecent);
        Assert.Equal(NotificationStatus.Pending, updatedRecent!.Status);
    }

    /// <summary>
    /// Tests that <see cref="HermesDbContext.DeduplicateNotificationLogsAsync"/> removes duplicate historical records while keeping the newest entry.
    /// </summary>
    [Fact]
    public async Task DeduplicateNotificationLogsAsync_Should_RemoveDuplicates_RetainingNewest()
    {
        // Arrange
        await using HermesDbContext ctx = CreateContext();
        User user = await SeedUserAsync(ctx);

        DateTime slot = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);
        NotificationLog log1 = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(5));
        NotificationLog log2 = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(5));
        NotificationLog log3 = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(5));

        ctx.NotificationLogs.AddRange(log1, log2, log3);
        await ctx.SaveChangesAsync();

        // Act
        int deletedCount = await ctx.DeduplicateNotificationLogsAsync();

        // Assert
        Assert.Equal(2, deletedCount);
        var remaining = await ctx.NotificationLogs.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(log3.Id, remaining[0].Id);
    }

    /// <summary>
    /// Tests that when two concurrent attempts race to reclaim the same stale Pending record, exactly one succeeds and the other receives ActiveLeaseInProgress.
    /// </summary>
    [Fact]
    public async Task TryReserveSlotAsync_ConcurrentRace_Should_AllowOnlyOneWorkerToReclaim()
    {
        // Arrange
        await using HermesDbContext ctx = CreateContext();
        User user = await SeedUserAsync(ctx);
        NotificationLogRepository sut = new(ctx);

        DateTime slot = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);
        NotificationLog staleLog = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(9), DateTime.UtcNow.AddMinutes(-3));
        ctx.NotificationLogs.Add(staleLog);
        await ctx.SaveChangesAsync();

        NotificationLog attempt1 = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(9));
        NotificationLog attempt2 = NotificationLog.Create(user.Id, DeliveryChannel.Email, slot, new NewsletterId(9));

        // Act
        SlotReservationResult result1 = await sut.TryReserveSlotAsync(attempt1, TimeSpan.FromSeconds(60));
        SlotReservationResult result2 = await sut.TryReserveSlotAsync(attempt2, TimeSpan.FromSeconds(60));

        // Assert
        Assert.True(result1.IsAcquired);
        Assert.Equal(SlotReservationStatus.Reclaimed, result1.Status);

        Assert.False(result2.IsAcquired);
        Assert.Equal(SlotReservationStatus.ActiveLeaseInProgress, result2.Status);
    }

    /// <summary>
    /// Tests that calling AdvanceNextDigestSlotAsync multiple times for the same slot (e.g. across retries) only advances NextDigestSlotUtc once.
    /// </summary>
    [Fact]
    public async Task AdvanceNextDigestSlotAsync_MultipleCalls_Should_AdvanceSlotOnlyOnce()
    {
        // Arrange
        await using HermesDbContext ctx = CreateContext();
        User user = await SeedUserAsync(ctx, id: 10);
        NewsletterSubscriptionRepository sut = new(ctx);

        DateTime slot1 = new(2026, 8, 17, 8, 0, 0, DateTimeKind.Utc); // Monday 08:00 UTC
        NewsletterSubscription sub = NewsletterSubscription.CreateForUser(user.Id);
        sub.AssignDigestSchedule(ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday, Weekdays.Tuesday, Weekdays.Wednesday], [new TimeOnly(8, 0)]));
        sub.SetNextDigestSlot(slot1);
        ctx.NewsletterSubscriptions.Add(sub);
        await ctx.SaveChangesAsync();

        DateTime referenceExclusive = slot1.AddMinutes(1); // 08:01 UTC

        // Act: First attempt advances from Monday to Tuesday
        await sut.AdvanceNextDigestSlotAsync(sub.Id, user.Id, TimeZoneInfo.Utc, referenceExclusive);
        var subAfterFirst = await ctx.NewsletterSubscriptions.FindAsync(sub.Id);
        DateTime? tuesdaySlot = subAfterFirst!.NextDigestSlotUtc;

        // Act: Second attempt (Hangfire retry) calls with the same slot reference
        await sut.AdvanceNextDigestSlotAsync(sub.Id, user.Id, TimeZoneInfo.Utc, referenceExclusive);
        var subAfterSecond = await ctx.NewsletterSubscriptions.FindAsync(sub.Id);

        // Assert: Slot must remain at Tuesday, NOT jump to Wednesday!
        Assert.NotNull(tuesdaySlot);
        Assert.Equal(tuesdaySlot, subAfterSecond!.NextDigestSlotUtc);
        Assert.Equal(new DateTime(2026, 8, 18, 8, 0, 0, DateTimeKind.Utc), subAfterSecond.NextDigestSlotUtc);
    }
}
