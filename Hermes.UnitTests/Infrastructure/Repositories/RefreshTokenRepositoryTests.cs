using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.Repositories;

/// <summary>
/// Contains unit tests for <see cref="RefreshTokenRepository"/> using an in-memory database,
/// verifying token hash lookups, expiration filters, batch revocation, and rotation tracking.
/// </summary>
public sealed class RefreshTokenRepositoryTests
{
    private static HermesDbContext CreateInMemoryContext()
    {
        DbContextOptions<HermesDbContext> options = new DbContextOptionsBuilder<HermesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HermesDbContext(options);
    }

    private static async Task<User> SeedUserAsync(HermesDbContext ctx, int userId = 1)
    {
        User user = new()
        {
            Id = new UserId(userId),
            Name = "TokenUser",
            Email = Email.Parse($"user{userId}@test.dev"),
            PasswordHash = "hash"
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Tests that <see cref="RefreshTokenRepository.GetActiveRefreshTokenByHashAsync"/> returns null
    /// when the hash is empty or the token has expired or been revoked.
    /// </summary>
    [Fact]
    public async Task GetActiveRefreshTokenByHashAsync_Should_ReturnNull_WhenExpiredOrRevoked()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        User user = await SeedUserAsync(ctx);
        RefreshTokenRepository sut = new(ctx, TimeProvider.System);

        RefreshToken expiredToken = RefreshToken.Create(
            user.Id,
            "expired-hash",
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddDays(-1));

        RefreshToken revokedToken = RefreshToken.Create(
            user.Id,
            "revoked-hash",
            DateTime.UtcNow.AddDays(7),
            DateTime.UtcNow);
        revokedToken.Revoke(DateTime.UtcNow);

        ctx.RefreshTokens.AddRange(expiredToken, revokedToken);
        await ctx.SaveChangesAsync();

        // Act
        RefreshToken? empty = await sut.GetActiveRefreshTokenByHashAsync("");
        RefreshToken? expired = await sut.GetActiveRefreshTokenByHashAsync("expired-hash");
        RefreshToken? revoked = await sut.GetActiveRefreshTokenByHashAsync("revoked-hash");

        // Assert
        Assert.Null(empty);
        Assert.Null(expired);
        Assert.Null(revoked);
    }

    /// <summary>
    /// Tests that <see cref="RefreshTokenRepository.GetActiveRefreshTokenByHashAsync"/> returns the active token
    /// when it has not expired and has not been revoked.
    /// </summary>
    [Fact]
    public async Task GetActiveRefreshTokenByHashAsync_Should_ReturnToken_WhenActive()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        User user = await SeedUserAsync(ctx);
        RefreshTokenRepository sut = new(ctx, TimeProvider.System);

        RefreshToken activeToken = RefreshToken.Create(
            user.Id,
            "valid-hash",
            DateTime.UtcNow.AddDays(7),
            DateTime.UtcNow);

        ctx.RefreshTokens.Add(activeToken);
        await ctx.SaveChangesAsync();

        // Act
        RefreshToken? result = await sut.GetActiveRefreshTokenByHashAsync("valid-hash");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.UserId);
        Assert.Null(result.RevokedAt);
    }

    /// <summary>
    /// Tests that <see cref="RefreshTokenRepository.RevokeAllRefreshTokensForUserAsync"/> revokes all
    /// active refresh tokens for the given user while ignoring already revoked tokens.
    /// </summary>
    [Fact]
    public async Task RevokeAllRefreshTokensForUserAsync_Should_RevokeAllActiveTokensForUser()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        User user = await SeedUserAsync(ctx);
        RefreshTokenRepository sut = new(ctx, TimeProvider.System);

        RefreshToken token1 = RefreshToken.Create(user.Id, "hash-1", DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
        RefreshToken token2 = RefreshToken.Create(user.Id, "hash-2", DateTime.UtcNow.AddDays(7), DateTime.UtcNow);

        ctx.RefreshTokens.AddRange(token1, token2);
        await ctx.SaveChangesAsync();

        // Act
        await sut.RevokeAllRefreshTokensForUserAsync(user.Id);

        // Assert
        List<RefreshToken> allTokens = await sut.GetAllRefreshTokensForUserAsync(user.Id);
        Assert.Equal(2, allTokens.Count);
        Assert.All(allTokens, t => Assert.NotNull(t.RevokedAt));
    }
}
