using Hermes.Application.DTOs.Security;
using Hermes.Application.Options.Auth;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Security;
using Hermes.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Security;

public sealed class AuthTokenServiceTests
{
    private static AuthTokenService CreateSut(
        Mock<IRefreshTokenRepository> db,
        Mock<IJwtTokenProvider> jwt,
        Mock<ILogger<AuthTokenService>>? logger = null,
        JwtOptions? options = null)
    {
        JwtOptions jwtOptions = options ?? new JwtOptions { RefreshTokenDays = 14 };
        return new AuthTokenService(db.Object, jwt.Object, Options.Create(jwtOptions), logger?.Object ?? new Mock<ILogger<AuthTokenService>>().Object);
    }

    [Fact]
    public async Task IssueTokensAsync_Should_PersistHashedRefresh_AndReturnPlainOnce()
    {
        Mock<IRefreshTokenRepository> db = new();
        Mock<IJwtTokenProvider> jwt = new();
        jwt.Setup(tokenIssuer => tokenIssuer.Issue(3, "a@test.example", "Alice"))
            .Returns(new JwtAccessTokenResultDto("access-jwt", DateTimeOffset.UtcNow.AddMinutes(30)));

        RefreshToken? captured = null;
        db.Setup(dataStore => dataStore.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((row, _) => captured = row)
            .Returns(Task.CompletedTask);

        AuthTokenService sut = CreateSut(db, jwt);
        AuthTokensResultDto result = await sut.IssueTokensAsync(3, "a@test.example", "Alice");
        Assert.Equal("access-jwt", result.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.NotNull(captured);
        Assert.Equal(3, captured!.UserId);
        Assert.Equal(RefreshTokenHashService.Hash(result.RefreshToken), captured.TokenHash);
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(3, "a@test.example", "Alice"), Times.Once);
        db.Verify(dataStore => dataStore.AddRefreshTokenAsync(captured, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task IssueTokensAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        AuthTokenService sut = CreateSut(new Mock<IRefreshTokenRepository>(), new Mock<IJwtTokenProvider>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.IssueTokensAsync(invalidUserId, "a@test.dev", "X"));
    }

    /// <summary>Blank refresh must not hit the store (timing side-channel).</summary>
    [Fact]
    public async Task RotateAsync_Should_NotTouchDatabase_WhenPlainMissingOrWhitespace()
    {
        Mock<IRefreshTokenRepository> db = new();
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());
        Assert.Null(await sut.RotateAsync(""));
        Assert.Null(await sut.RotateAsync("   "));
        db.Verify(dataStore => dataStore.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_Should_ReturnNull_WhenNoActiveRefreshMatchesHash()
    {
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());
        AuthTokensResultDto? result = await sut.RotateAsync("orphan-plain");
        Assert.Null(result);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_Should_Abort_WhenStoredSessionHasNoUserNavigation()
    {
        string plain = "token";
        string hash = RefreshTokenHashService.Hash(plain);
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken { UserId = 1, TokenHash = hash, User = null });
        Mock<IJwtTokenProvider> jwt = new();
        AuthTokenService sut = CreateSut(db, jwt);
        Assert.Null(await sut.RotateAsync(plain));
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_Should_CompleteRotation_WithNewRefreshMaterial_AndRevokeOldPlain()
    {
        string plainOld = "old-refresh-plain";
        string hashOld = RefreshTokenHashService.Hash(plainOld);
        RefreshToken oldRow = new()
        {
            Id = 10,
            UserId = 7,
            TokenHash = hashOld,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            User = new User { Id = 7, Email = "u@example.org", Name = "Uwe" },
        };

        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRow);
        db.Setup(dataStore => dataStore.CompleteRefreshRotationAsync(
                It.IsAny<RefreshToken>(),
                It.IsAny<RefreshToken>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock<IJwtTokenProvider> jwt = new();
        jwt.Setup(tokenIssuer => tokenIssuer.Issue(7, "u@example.org", "Uwe"))
            .Returns(new JwtAccessTokenResultDto("new-access", DateTimeOffset.UtcNow.AddMinutes(20)));

        AuthTokenService sut = CreateSut(db, jwt);
        AuthTokensResultDto? result = await sut.RotateAsync(plainOld);
        Assert.NotNull(result);
        Assert.Equal("new-access", result!.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));

        jwt.Verify(tokenIssuer => tokenIssuer.Issue(7, "u@example.org", "Uwe"), Times.Once);
        db.Verify(
            dataStore => dataStore.CompleteRefreshRotationAsync(
                oldRow,
                It.Is<RefreshToken>(nr => nr.UserId == 7 && nr.TokenHash == RefreshTokenHashService.Hash(result.RefreshToken)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RotateAsync_Should_ReturnNull_WhenRotationNotClaimed()
    {
        string plainOld = "concurrent-loser";
        string hashOld = RefreshTokenHashService.Hash(plainOld);
        RefreshToken oldRow = new()
        {
            Id = 99,
            UserId = 7,
            TokenHash = hashOld,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            User = new User { Id = 7, Email = "u@example.org", Name = "Uwe" },
        };

        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRow);
        db.Setup(dataStore => dataStore.CompleteRefreshRotationAsync(
                It.IsAny<RefreshToken>(),
                It.IsAny<RefreshToken>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IJwtTokenProvider> jwt = new();
        AuthTokenService sut = CreateSut(db, jwt);

        Assert.Null(await sut.RotateAsync(plainOld));

        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_Should_RevokeFamily_WhenReplayDetected()
    {
        string plainOld = "revoked-refresh-plain";
        string hashOld = RefreshTokenHashService.Hash(plainOld);
        RefreshToken oldRow = new()
        {
            Id = 10,
            UserId = 7,
            TokenHash = hashOld,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow.AddMinutes(-5),
            User = new User { Id = 7, Email = "u@example.org", Name = "Uwe" },
        };

        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRow);

        Mock<ILogger<AuthTokenService>> logger = new();
        Mock<IJwtTokenProvider> jwt = new();

        AuthTokenService sut = CreateSut(db, jwt, logger);
        AuthTokensResultDto? result = await sut.RotateAsync(plainOld);
        Assert.Null(result);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(dataStore => dataStore.RevokeTokenFamilyAsync(oldRow, It.IsAny<CancellationToken>()), Times.Once);
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_Should_RevokeFamily_WhenExpired_ButNotExplicitlyRevoked()
    {
        // Arrange
        string plainOld = "expired-refresh-plain";
        string hashOld = RefreshTokenHashService.Hash(plainOld);
        RefreshToken oldRow = new()
        {
            Id = 11,
            UserId = 8,
            TokenHash = hashOld,
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow.AddDays(-15),
            RevokedAt = null,
            User = new User { Id = 8, Email = "e@example.org", Name = "E" },
        };

        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRow);

        Mock<ILogger<AuthTokenService>> logger = new();
        Mock<IJwtTokenProvider> jwt = new();

        AuthTokenService sut = CreateSut(db, jwt, logger);

        // Act
        AuthTokensResultDto? result = await sut.RotateAsync(plainOld);

        // Assert
        Assert.Null(result);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(dataStore => dataStore.RevokeTokenFamilyAsync(oldRow, It.IsAny<CancellationToken>()), Times.Once);
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TryRevokeRefreshForUserAsync_Should_ReturnFalse_WhenTokenBlank(string? blankToken)
    {
        // Arrange
        Mock<IRefreshTokenRepository> db = new();
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());

        // Act
        bool result = await sut.TryRevokeRefreshForUserAsync(blankToken!, 1);

        // Assert
        Assert.False(result);
        db.Verify(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryRevokeRefreshForUserAsync_Should_ReturnFalse_WhenTokenNotFound()
    {
        // Arrange
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());

        // Act
        bool result = await sut.TryRevokeRefreshForUserAsync("unknown-token", 1);

        // Assert
        Assert.False(result);
        db.Verify(dataStore => dataStore.RevokeRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Revocation is user-scoped, not plaintext-hash alone.</summary>
    [Fact]
    public async Task TryRevokeRefreshForUserAsync_Should_NotRevokeForeignSession()
    {
        string plain = "secret";
        string hash = RefreshTokenHashService.Hash(plain);
        RefreshToken row = new() { UserId = 5, TokenHash = hash };
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());
        Assert.False(await sut.TryRevokeRefreshForUserAsync(plain, 99));
        db.Verify(dataStore => dataStore.RevokeRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryRevokeRefreshForUserAsync_Should_Revoke_WhenHashMatchesAuthenticatedUser()
    {
        string plain = "secret";
        string hash = RefreshTokenHashService.Hash(plain);
        RefreshToken row = new() { UserId = 12, TokenHash = hash };
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        db.Setup(dataStore => dataStore.RevokeRefreshTokenAsync(row, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());
        Assert.True(await sut.TryRevokeRefreshForUserAsync(plain, 12));
        db.Verify(dataStore => dataStore.RevokeRefreshTokenAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAllForUserAsync_Should_DelegateToStore()
    {
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.RevokeAllRefreshTokensForUserAsync(44, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());
        await sut.RevokeAllForUserAsync(44);
        db.Verify(dataStore => dataStore.RevokeAllRefreshTokensForUserAsync(44, It.IsAny<CancellationToken>()), Times.Once);
    }
}
