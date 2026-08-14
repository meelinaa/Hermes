using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.Data;

public sealed class HermesDbContextTests
{
    private static HermesDbContext CreateInMemoryContext()
    {
        DbContextOptions<HermesDbContext> options = new DbContextOptionsBuilder<HermesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HermesDbContext(options);
    }

    private static async Task<User> SeedUserAsync(HermesDbContext ctx)
    {
        User seededUser = new()
        {
            Id = new UserId(Random.Shared.Next(1, 10000)),
            Name = "Tester",
            Email = Email.Parse("db@test.example"),
            PasswordHash = "$2a$placeholder",
        };
        ctx.Users.Add(seededUser);
        await ctx.SaveChangesAsync().ConfigureAwait(false);
        return seededUser;
    }

    [Fact]
    public async Task ExistsSentNotificationInWindowAsync_ReturnsTrue_WhenSentRowInsideHalfOpenWindow()
    {
        await using HermesDbContext ctx = CreateInMemoryContext();
        var logStore = new NotificationLogRepository(ctx);
        User user = await SeedUserAsync(ctx);

        DateTime windowStart = new(2026, 4, 10, 8, 15, 0, DateTimeKind.Utc);
        DateTime windowEnd = windowStart.AddMinutes(1);

        var log1 = NotificationLog.Create(user.Id, DeliveryChannel.Email, windowStart.AddSeconds(40), new NewsletterId(42));
        log1.MarkAsSent();
        ctx.NotificationLogs.Add(log1);
        await ctx.SaveChangesAsync();

        bool exists = await logStore.ExistsSentNotificationInWindowAsync(user.Id, new NewsletterId(42), windowStart, windowEnd, CancellationToken.None);

        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsSentNotificationInWindowAsync_ReturnsFalse_WhenOutsideWindowOrWrongStatus()
    {
        await using HermesDbContext ctx = CreateInMemoryContext();
        var logStore = new NotificationLogRepository(ctx);
        User user = await SeedUserAsync(ctx);

        DateTime windowStart = new(2026, 4, 10, 8, 15, 0, DateTimeKind.Utc);
        DateTime windowEnd = windowStart.AddMinutes(1);

        var log2 = NotificationLog.Create(user.Id, DeliveryChannel.Email, windowStart.AddMinutes(-5), new NewsletterId(1));
        log2.MarkAsSent();
        ctx.NotificationLogs.Add(log2);

        var log3 = NotificationLog.Create(user.Id, DeliveryChannel.Email, windowStart.AddSeconds(20), new NewsletterId(2));
        log3.MarkAsFailed("error", null);
        ctx.NotificationLogs.Add(log3);

        var log4 = NotificationLog.Create(user.Id, DeliveryChannel.Email, windowEnd, new NewsletterId(3));
        log4.MarkAsSent();
        ctx.NotificationLogs.Add(log4);
        await ctx.SaveChangesAsync();

        Assert.False(await logStore.ExistsSentNotificationInWindowAsync(user.Id, new NewsletterId(1), windowStart, windowEnd, CancellationToken.None));
        Assert.False(await logStore.ExistsSentNotificationInWindowAsync(user.Id, new NewsletterId(2), windowStart, windowEnd, CancellationToken.None));
        Assert.False(await logStore.ExistsSentNotificationInWindowAsync(user.Id, new NewsletterId(3), windowStart, windowEnd, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteRefreshRotationAsync_SetsRevokedAndReplacementLink()
    {
        await using HermesDbContext ctx = CreateInMemoryContext();
        var tokens = new RefreshTokenRepository(ctx, TimeProvider.System);
        User user = await SeedUserAsync(ctx);

        RefreshToken oldToken = RefreshToken.Create(
            user.Id,
            "hash-old-test",
            DateTime.UtcNow.AddDays(7),
            DateTime.UtcNow);
        ctx.RefreshTokens.Add(oldToken);
        await ctx.SaveChangesAsync();

        RefreshToken newToken = RefreshToken.Create(
            user.Id,
            "hash-new-test",
            DateTime.UtcNow.AddDays(14),
            DateTime.UtcNow);

        bool ok = await tokens.CompleteRefreshRotationAsync(oldToken, newToken, CancellationToken.None);

        Assert.True(ok);
        Assert.True(oldToken.RevokedAt.HasValue);
        Assert.Equal(newToken.Id, oldToken.ReplacedByTokenId);

        RefreshToken persistedOld = await ctx.RefreshTokens.AsNoTracking().FirstAsync(refreshToken => refreshToken.Id == oldToken.Id);
        RefreshToken persistedNew = await ctx.RefreshTokens.AsNoTracking().FirstAsync(refreshToken => refreshToken.Id == newToken.Id);

        Assert.True(persistedOld.RevokedAt.HasValue);
        Assert.Equal(persistedNew.Id, persistedOld.ReplacedByTokenId);
    }

    [Fact]
    public async Task GetActiveRefreshTokenByHashAsync_Should_ReturnNull_WhenHashEmpty()
    {
        await using HermesDbContext ctx = CreateInMemoryContext();
        var tokens = new RefreshTokenRepository(ctx, TimeProvider.System);

        RefreshToken? row = await tokens.GetActiveRefreshTokenByHashAsync("", CancellationToken.None);

        Assert.Null(row);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_ClearIsEmailVerified_WhenEmailChanges()
    {
        await using HermesDbContext ctx = CreateInMemoryContext();
        var users = new UserRepository(ctx);
        User user = await SeedUserAsync(ctx);
        user.IsEmailVerified = true;
        await ctx.SaveChangesAsync();

        User patch = new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = Email.Parse("new-email@test.example"),
            PasswordHash = null,
        };

        await users.UpdateUserAsync(patch, CancellationToken.None);

        User? reloaded = await ctx.Users.AsNoTracking().FirstOrDefaultAsync(userEntity => userEntity.Id == user.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("new-email@test.example", reloaded!.Email);
        Assert.False(reloaded.IsEmailVerified);
    }
}

