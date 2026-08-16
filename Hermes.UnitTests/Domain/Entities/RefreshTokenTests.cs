using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Hermes.UnitTests.Domain.Entities;

/// <summary>
/// Contains unit tests for the <see cref="RefreshToken"/> domain entity,
/// testing session activation, expiration boundaries, and revocation chains.
/// </summary>
public sealed class RefreshTokenTests
{
    /// <summary>
    /// Tests that <see cref="RefreshToken.Create"/> correctly initializes property values.
    /// </summary>
    [Fact]
    public void Create_Should_InitializePropertiesCorrectly()
    {
        // Arrange
        UserId userId = new(42);
        string tokenHash = "sha256hashvalue";
        DateTime createdAt = new(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);
        DateTime expiresAt = createdAt.AddDays(7);

        // Act
        RefreshToken token = RefreshToken.Create(userId, tokenHash, expiresAt, createdAt);

        // Assert
        Assert.Equal(userId, token.UserId);
        Assert.Equal(tokenHash, token.TokenHash);
        Assert.Equal(createdAt, token.CreatedAt);
        Assert.Equal(expiresAt, token.ExpiresAt);
        Assert.Null(token.RevokedAt);
        Assert.Null(token.ReplacedByTokenId);
        Assert.False(token.IsRevoked);
    }

    /// <summary>
    /// Tests that <see cref="RefreshToken.IsExpired"/> returns true when current UTC time is greater than or equal to <see cref="RefreshToken.ExpiresAt"/>.
    /// </summary>
    [Theory]
    [InlineData(0, true)]   // Exactly at expiration instant
    [InlineData(1, true)]   // 1 second after expiration
    [InlineData(-1, false)] // 1 second before expiration
    public void IsExpired_Should_ReturnExpectedResult_DependingOnCurrentTime(int secondsOffsetFromExpiry, bool expectedExpired)
    {
        // Arrange
        DateTime expiresAt = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);
        RefreshToken token = RefreshToken.Create(new UserId(1), "hash", expiresAt, expiresAt.AddDays(-7));
        var timeProvider = new FakeTimeProvider(expiresAt.AddSeconds(secondsOffsetFromExpiry));

        // Act
        bool isExpired = token.IsExpired(timeProvider);

        // Assert
        Assert.Equal(expectedExpired, isExpired);
    }

    /// <summary>
    /// Tests that <see cref="RefreshToken.IsRevoked"/> returns true when a revocation timestamp is present.
    /// </summary>
    [Fact]
    public void IsRevoked_Should_ReturnTrue_WhenRevokedAtIsSet()
    {
        // Arrange
        RefreshToken token = RefreshToken.Create(new UserId(1), "hash", DateTime.UtcNow.AddDays(1), DateTime.UtcNow);
        Assert.False(token.IsRevoked);

        // Act
        token.Revoke(DateTime.UtcNow, replacedByTokenId: 99);

        // Assert
        Assert.True(token.IsRevoked);
        Assert.NotNull(token.RevokedAt);
        Assert.Equal(99, token.ReplacedByTokenId);
    }

    /// <summary>
    /// Tests that <see cref="RefreshToken.IsActive"/> returns true only when the token is neither revoked nor expired.
    /// </summary>
    [Fact]
    public void IsActive_Should_ReturnTrue_WhenNotRevokedAndNotExpired()
    {
        // Arrange
        DateTime now = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider(now);
        RefreshToken token = RefreshToken.Create(new UserId(1), "hash", now.AddDays(7), now);

        // Act & Assert
        Assert.True(token.IsActive(timeProvider));
    }

    /// <summary>
    /// Tests that <see cref="RefreshToken.IsActive"/> returns false when the token is revoked, even if unexpired.
    /// </summary>
    [Fact]
    public void IsActive_Should_ReturnFalse_WhenRevokedEvenIfUnexpired()
    {
        // Arrange
        DateTime now = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider(now);
        RefreshToken token = RefreshToken.Create(new UserId(1), "hash", now.AddDays(7), now);
        token.Revoke(now);

        // Act & Assert
        Assert.False(token.IsActive(timeProvider));
    }

    /// <summary>
    /// Tests that <see cref="RefreshToken.IsActive"/> returns false when the token is expired, even if not revoked.
    /// </summary>
    [Fact]
    public void IsActive_Should_ReturnFalse_WhenExpired()
    {
        // Arrange
        DateTime now = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider(now);
        RefreshToken token = RefreshToken.Create(new UserId(1), "hash", now.AddMinutes(-5), now.AddDays(-7));

        // Act & Assert
        Assert.False(token.IsActive(timeProvider));
    }
}
