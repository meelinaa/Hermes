using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Security;
using Hermes.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Security;

public sealed class AuthTokenServiceTests
{
    private static AuthTokenService CreateSut(
        Mock<IRefreshTokenStore> db,
        Mock<IJwtTokenIssuer> jwt,
        Mock<ILogger<AuthTokenService>>? logger = null,
        JwtOptions? options = null)
    {
        JwtOptions jwtOptions = options ?? new JwtOptions { RefreshTokenDays = 14 };
        return new AuthTokenService(db.Object, jwt.Object, Options.Create(jwtOptions), logger?.Object ?? new Mock<ILogger<AuthTokenService>>().Object);
    }

    [Fact]
    public async Task IssueTokensAsync_Should_PersistHashedRefresh_AndReturnPlainOnce()
    {
        Mock<IRefreshTokenStore> db = new();
        Mock<IJwtTokenIssuer> jwt = new();
        jwt.Setup(tokenIssuer => tokenIssuer.Issue(3, "a@test.example", "Alice"))
            .Returns(new JwtAccessTokenResult("access-jwt", DateTimeOffset.UtcNow.AddMinutes(30)));

        RefreshToken? captured = null;
        db.Setup(dataStore => dataStore.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((row, _) => captured = row)
            .Returns(Task.CompletedTask);

        AuthTokenService sut = CreateSut(db, jwt);
        AuthTokensResult result = await sut.IssueTokensAsync(3, "a@test.example", "Alice");
        Assert.Equal("access-jwt", result.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.NotNull(captured);
        Assert.Equal(3, captured!.UserId);
        Assert.Equal(RefreshTokenHasher.Hash(result.RefreshToken), captured.TokenHash);
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(3, "a@test.example", "Alice"), Times.Once);
        db.Verify(dataStore => dataStore.AddRefreshTokenAsync(captured, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task IssueTokensAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        AuthTokenService sut = CreateSut(new Mock<IRefreshTokenStore>(), new Mock<IJwtTokenIssuer>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.IssueTokensAsync(invalidUserId, "a@test.dev", "X"));
    }

    /// <summary>Blank refresh must not hit the store (timing side-channel).</summary>
    [Fact]
    public async Task RotateAsync_Should_NotTouchDatabase_WhenPlainMissingOrWhitespace()
    {
        Mock<IRefreshTokenStore> db = new();
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenIssuer>());
        Assert.Null(await sut.RotateAsync(""));
        Assert.Null(await sut.RotateAsync("   "));
        db.Verify(dataStore => dataStore.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_Should_ReturnNull_WhenNoActiveRefreshMatchesHash()
    {
        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenIssuer>());
        AuthTokensResult? result = await sut.RotateAsync("orphan-plain");
        Assert.Null(result);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_Should_Abort_WhenStoredSessionHasNoUserNavigation()
    {
        string plain = "token";
        string hash = RefreshTokenHasher.Hash(plain);
        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken { UserId = 1, TokenHash = hash, User = null });
        Mock<IJwtTokenIssuer> jwt = new();
        AuthTokenService sut = CreateSut(db, jwt);
        Assert.Null(await sut.RotateAsync(plain));
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_Should_CompleteRotation_WithNewRefreshMaterial_AndRevokeOldPlain()
    {
        string plainOld = "old-refresh-plain";
        string hashOld = RefreshTokenHasher.Hash(plainOld);
        RefreshToken oldRow = new()
        {
            Id = 10,
            UserId = 7,
            TokenHash = hashOld,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            User = new User { Id = 7, Email = "u@example.org", Name = "Uwe" },
        };

        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRow);
        db.Setup(dataStore => dataStore.CompleteRefreshRotationAsync(
                It.IsAny<RefreshToken>(),
                It.IsAny<RefreshToken>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock<IJwtTokenIssuer> jwt = new();
        jwt.Setup(tokenIssuer => tokenIssuer.Issue(7, "u@example.org", "Uwe"))
            .Returns(new JwtAccessTokenResult("new-access", DateTimeOffset.UtcNow.AddMinutes(20)));

        AuthTokenService sut = CreateSut(db, jwt);
        AuthTokensResult? result = await sut.RotateAsync(plainOld);
        Assert.NotNull(result);
        Assert.Equal("new-access", result!.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));

        jwt.Verify(tokenIssuer => tokenIssuer.Issue(7, "u@example.org", "Uwe"), Times.Once);
        db.Verify(
            dataStore => dataStore.CompleteRefreshRotationAsync(
                oldRow,
                It.Is<RefreshToken>(nr => nr.UserId == 7 && nr.TokenHash == RefreshTokenHasher.Hash(result.RefreshToken)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RotateAsync_Should_ReturnNull_WhenRotationNotClaimed()
    {
        string plainOld = "concurrent-loser";
        string hashOld = RefreshTokenHasher.Hash(plainOld);
        RefreshToken oldRow = new()
        {
            Id = 99,
            UserId = 7,
            TokenHash = hashOld,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            User = new User { Id = 7, Email = "u@example.org", Name = "Uwe" },
        };

        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRow);
        db.Setup(dataStore => dataStore.CompleteRefreshRotationAsync(
                It.IsAny<RefreshToken>(),
                It.IsAny<RefreshToken>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IJwtTokenIssuer> jwt = new();
        AuthTokenService sut = CreateSut(db, jwt);

        Assert.Null(await sut.RotateAsync(plainOld));

        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RotateAsync_Should_RevokeFamily_WhenReplayDetected()
    {
        string plainOld = "revoked-refresh-plain";
        string hashOld = RefreshTokenHasher.Hash(plainOld);
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

        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRow);

        Mock<ILogger<AuthTokenService>> logger = new();
        Mock<IJwtTokenIssuer> jwt = new();

        AuthTokenService sut = CreateSut(db, jwt, logger);
        AuthTokensResult? result = await sut.RotateAsync(plainOld);
        Assert.Null(result);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(dataStore => dataStore.RevokeTokenFamilyAsync(oldRow, It.IsAny<CancellationToken>()), Times.Once);
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>Revocation is user-scoped, not plaintext-hash alone.</summary>
    [Fact]
    public async Task TryRevokeRefreshForUserAsync_Should_NotRevokeForeignSession()
    {
        string plain = "secret";
        string hash = RefreshTokenHasher.Hash(plain);
        RefreshToken row = new() { UserId = 5, TokenHash = hash };
        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenIssuer>());
        Assert.False(await sut.TryRevokeRefreshForUserAsync(plain, 99));
        db.Verify(dataStore => dataStore.RevokeRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryRevokeRefreshForUserAsync_Should_Revoke_WhenHashMatchesAuthenticatedUser()
    {
        string plain = "secret";
        string hash = RefreshTokenHasher.Hash(plain);
        RefreshToken row = new() { UserId = 12, TokenHash = hash };
        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        db.Setup(dataStore => dataStore.RevokeRefreshTokenAsync(row, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenIssuer>());
        Assert.True(await sut.TryRevokeRefreshForUserAsync(plain, 12));
        db.Verify(dataStore => dataStore.RevokeRefreshTokenAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAllForUserAsync_Should_DelegateToStore()
    {
        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.RevokeAllRefreshTokensForUserAsync(44, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenIssuer>());
        await sut.RevokeAllForUserAsync(44);
        db.Verify(dataStore => dataStore.RevokeAllRefreshTokensForUserAsync(44, It.IsAny<CancellationToken>()), Times.Once);
    }
}
