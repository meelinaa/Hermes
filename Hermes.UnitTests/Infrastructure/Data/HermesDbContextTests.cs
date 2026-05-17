using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Infrastructure.Data;
using Hermes.Infrastructure.Repositories;
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
            Name = "Tester",
            Email = "db@test.example",
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
        var logStore = new NotificationLogStore(ctx);
        User user = await SeedUserAsync(ctx);

        DateTime windowStart = new(2026, 4, 10, 8, 15, 0, DateTimeKind.Utc);
        DateTime windowEnd = windowStart.AddMinutes(1);

        ctx.NotificationLogs.Add(new NotificationLog
        {
            UserId = user.Id,
            NewsId = 42,
            SentAt = windowStart.AddSeconds(40),
            Status = NotificationStatus.Sent,
            Channel = DeliveryChannel.Email,
        });
        await ctx.SaveChangesAsync();

        bool exists = await logStore.ExistsSentNotificationInWindowAsync(user.Id, 42, windowStart, windowEnd, CancellationToken.None);

        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsSentNotificationInWindowAsync_ReturnsFalse_WhenOutsideWindowOrWrongStatus()
    {
        await using HermesDbContext ctx = CreateInMemoryContext();
        var logStore = new NotificationLogStore(ctx);
        User user = await SeedUserAsync(ctx);

        DateTime windowStart = new(2026, 4, 10, 8, 15, 0, DateTimeKind.Utc);
        DateTime windowEnd = windowStart.AddMinutes(1);

        ctx.NotificationLogs.Add(new NotificationLog
        {
            UserId = user.Id,
            NewsId = 1,
            SentAt = windowStart.AddMinutes(-5),
            Status = NotificationStatus.Sent,
            Channel = DeliveryChannel.Email,
        });
        ctx.NotificationLogs.Add(new NotificationLog
        {
            UserId = user.Id,
            NewsId = 2,
            SentAt = windowStart.AddSeconds(20),
            Status = NotificationStatus.Failed,
            Channel = DeliveryChannel.Email,
        });
        ctx.NotificationLogs.Add(new NotificationLog
        {
            UserId = user.Id,
            NewsId = 3,
            SentAt = windowEnd,
            Status = NotificationStatus.Sent,
            Channel = DeliveryChannel.Email,
        });
        await ctx.SaveChangesAsync();

        Assert.False(await logStore.ExistsSentNotificationInWindowAsync(user.Id, 1, windowStart, windowEnd, CancellationToken.None));
        Assert.False(await logStore.ExistsSentNotificationInWindowAsync(user.Id, 2, windowStart, windowEnd, CancellationToken.None));
        Assert.False(await logStore.ExistsSentNotificationInWindowAsync(user.Id, 3, windowStart, windowEnd, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteRefreshRotationAsync_SetsRevokedAndReplacementLink()
    {
        await using HermesDbContext ctx = CreateInMemoryContext();
        var tokens = new RefreshTokenStore(ctx);
        User user = await SeedUserAsync(ctx);

        RefreshToken oldToken = new()
        {
            UserId = user.Id,
            TokenHash = "hash-old-test",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
        };
        ctx.RefreshTokens.Add(oldToken);
        await ctx.SaveChangesAsync();

        RefreshToken newToken = new()
        {
            UserId = user.Id,
            TokenHash = "hash-new-test",
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            CreatedAt = DateTime.UtcNow,
        };

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
        var tokens = new RefreshTokenStore(ctx);

        RefreshToken? row = await tokens.GetActiveRefreshTokenByHashAsync("", CancellationToken.None);

        Assert.Null(row);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_ClearIsEmailVerified_WhenEmailChanges()
    {
        await using HermesDbContext ctx = CreateInMemoryContext();
        var users = new UserStore(ctx);
        User user = await SeedUserAsync(ctx);
        user.IsEmailVerified = true;
        await ctx.SaveChangesAsync();

        User patch = new()
        {
            Id = user.Id,
            Name = user.Name,
            Email = "new-email@test.example",
            PasswordHash = null,
        };

        await users.UpdateUserAsync(patch, CancellationToken.None);

        User? reloaded = await ctx.Users.AsNoTracking().FirstOrDefaultAsync(userEntity => userEntity.Id == user.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("new-email@test.example", reloaded!.Email);
        Assert.False(reloaded.IsEmailVerified);
    }
}
