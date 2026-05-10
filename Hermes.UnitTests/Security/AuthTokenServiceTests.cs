using System.Security.Cryptography;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Security;
using Hermes.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Security;

/// <summary>
/// Specifications for refresh-token lifecycle: hashed storage, replay-safe rotation, user scoping, and safe early exits on bad input.
/// </summary>
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

    /// <summary>
    /// On successful issuance, persist only the hash of the refresh token; return the plaintext refresh once; delegate JWT creation to <see cref="IJwtTokenIssuer"/>.
    /// </summary>
    [Fact]
    public async Task IssueTokensAsync_Should_PersistHashedRefresh_AndReturnPlainOnce()
    {
        // Arrange
        Mock<IRefreshTokenStore> db = new();
        Mock<IJwtTokenIssuer> jwt = new();
        jwt.Setup(tokenIssuer => tokenIssuer.Issue(3, "a@test.example", "Alice"))
            .Returns(new JwtAccessTokenResult("access-jwt", DateTimeOffset.UtcNow.AddMinutes(30)));

        RefreshToken? captured = null;
        db.Setup(dataStore => dataStore.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((row, _) => captured = row)
            .Returns(Task.CompletedTask);

        AuthTokenService sut = CreateSut(db, jwt);

        // Act
        AuthTokensResult result = await sut.IssueTokensAsync(3, "a@test.example", "Alice");

        // Assert
        Assert.Equal("access-jwt", result.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.NotNull(captured);
        Assert.Equal(3, captured!.UserId);
        Assert.Equal(RefreshTokenHasher.Hash(result.RefreshToken), captured.TokenHash);
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(3, "a@test.example", "Alice"), Times.Once);
        db.Verify(dataStore => dataStore.AddRefreshTokenAsync(captured, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// User id must be positive before touching JWT or database.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task IssueTokensAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        AuthTokenService sut = CreateSut(new Mock<IRefreshTokenStore>(), new Mock<IJwtTokenIssuer>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.IssueTokensAsync(invalidUserId, "a@test.dev", "X"));
    }

    /// <summary>
    /// Empty or whitespace refresh material cannot match a hash — skip DB lookups entirely (no timing oracle via DB).
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_NotTouchDatabase_WhenPlainMissingOrWhitespace()
    {
        // Arrange
        Mock<IRefreshTokenStore> db = new();
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenIssuer>());

        // Act
        Assert.Null(await sut.RotateAsync(""));
        Assert.Null(await sut.RotateAsync("   "));

        // Assert
        db.Verify(dataStore => dataStore.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Unknown or revoked hash yields null and must not start rotation transaction.
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_ReturnNull_WhenNoActiveRefreshMatchesHash()
    {
        // Arrange
        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenIssuer>());

        // Act
        AuthTokensResult? result = await sut.RotateAsync("orphan-plain");

        // Assert
        Assert.Null(result);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Defensive guard: if the ORM returns a refresh row without user navigation loaded, rotation aborts (cannot issue new JWT).
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_Abort_WhenStoredSessionHasNoUserNavigation()
    {
        // Arrange
        string plain = "token";
        string hash = RefreshTokenHasher.Hash(plain);
        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken { UserId = 1, TokenHash = hash, User = null });
        Mock<IJwtTokenIssuer> jwt = new();
        AuthTokenService sut = CreateSut(db, jwt);

        // Act
        Assert.Null(await sut.RotateAsync(plain));

        // Assert
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Happy path: hash matches active row → issue new access + new refresh, complete rotation (revoke old, link replacement).
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_CompleteRotation_WithNewRefreshMaterial_AndRevokeOldPlain()
    {
        // Arrange
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

        // Act
        AuthTokensResult? result = await sut.RotateAsync(plainOld);

        // Assert
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

    /// <summary>
    /// Another request won refresh rotation (e.g. duplicate tab) — abort without issuing an access token.
    /// </summary>
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

    /// <summary>
    /// Replay scenario: using a revoked token revokes the entire token family.
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_RevokeFamily_WhenReplayDetected()
    {
        // Arrange
        string plainOld = "revoked-refresh-plain";
        string hashOld = RefreshTokenHasher.Hash(plainOld);
        RefreshToken oldRow = new()
        {
            Id = 10,
            UserId = 7,
            TokenHash = hashOld,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow.AddMinutes(-5), // Token is revoked!
            User = new User { Id = 7, Email = "u@example.org", Name = "Uwe" },
        };

        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRow);
        
        Mock<ILogger<AuthTokenService>> logger = new();
        Mock<IJwtTokenIssuer> jwt = new();

        AuthTokenService sut = CreateSut(db, jwt, logger);

        // Act
        AuthTokensResult? result = await sut.RotateAsync(plainOld);

        // Assert
        Assert.Null(result); // Rotation should fail
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(dataStore => dataStore.RevokeTokenFamilyAsync(oldRow, It.IsAny<CancellationToken>()), Times.Once); // Family revoked
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>
    /// Revocation by user id must not revoke another user's session even if the plaintext matches (hash collision prevented by user scope).
    /// </summary>
    [Fact]
    public async Task TryRevokeRefreshForUserAsync_Should_NotRevokeForeignSession()
    {
        // Arrange
        string plain = "secret";
        string hash = RefreshTokenHasher.Hash(plain);
        RefreshToken row = new() { UserId = 5, TokenHash = hash };
        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenIssuer>());

        // Act
        Assert.False(await sut.TryRevokeRefreshForUserAsync(plain, 99));

        // Assert
        db.Verify(dataStore => dataStore.RevokeRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// When hash matches and authenticated user id equals token owner, revoke the refresh row.
    /// </summary>
    [Fact]
    public async Task TryRevokeRefreshForUserAsync_Should_Revoke_WhenHashMatchesAuthenticatedUser()
    {
        // Arrange
        string plain = "secret";
        string hash = RefreshTokenHasher.Hash(plain);
        RefreshToken row = new() { UserId = 12, TokenHash = hash };
        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        db.Setup(dataStore => dataStore.RevokeRefreshTokenAsync(row, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenIssuer>());

        // Act
        Assert.True(await sut.TryRevokeRefreshForUserAsync(plain, 12));

        // Assert
        db.Verify(dataStore => dataStore.RevokeRefreshTokenAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Bulk revoke delegates to the store for the given user id (e.g. logout-all-devices).
    /// </summary>
    [Fact]
    public async Task RevokeAllForUserAsync_Should_DelegateToStore()
    {
        // Arrange
        Mock<IRefreshTokenStore> db = new();
        db.Setup(dataStore => dataStore.RevokeAllRefreshTokensForUserAsync(44, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenIssuer>());

        // Act
        await sut.RevokeAllForUserAsync(44);

        // Assert
        db.Verify(dataStore => dataStore.RevokeAllRefreshTokensForUserAsync(44, It.IsAny<CancellationToken>()), Times.Once);
    }
}
