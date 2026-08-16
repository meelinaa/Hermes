using FluentResults;
using Hermes.Application.DTOs.Security;
using Hermes.Application.Errors;
using Hermes.Application.Options.Auth;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Security;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Security;

/// <summary>
/// Contains unit tests for <see cref="AuthTokenService"/>, validating token issuance,
/// cryptographically secure refresh token rotation, replay attack family revocation, and user session invalidation.
/// </summary>
public sealed class AuthTokenServiceTests
{
    /// <summary>
    /// Helper method to instantiate <see cref="AuthTokenService"/> with mocked dependencies and optional custom time/logger.
    /// </summary>
    private static AuthTokenService CreateSut(
        Mock<IRefreshTokenRepository> db,
        Mock<IJwtTokenProvider> jwt,
        Mock<ILogger<AuthTokenService>>? logger = null,
        JwtOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        JwtOptions jwtOptions = options ?? new JwtOptions { RefreshTokenDays = 14 };
        return new AuthTokenService(
            db.Object,
            jwt.Object,
            Options.Create(jwtOptions),
            timeProvider ?? TimeProvider.System,
            logger?.Object ?? Mock.Of<ILogger<AuthTokenService>>());
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.IssueTokensAsync"/> generates a JWT access token,
    /// persists a SHA-256 hashed refresh token with the configured validity, and returns plaintext tokens.
    /// </summary>
    [Fact]
    public async Task IssueTokensAsync_Should_PersistHashedRefresh_AndReturnPlainOnce()
    {
        // Arrange
        Mock<IRefreshTokenRepository> db = new();
        Mock<IJwtTokenProvider> jwt = new();
        jwt.Setup(tokenIssuer => tokenIssuer.Issue(new UserId(3), "a@test.example", "Alice"))
            .Returns(new JwtAccessTokenResultDto("access-jwt", DateTimeOffset.UtcNow.AddMinutes(30)));

        RefreshToken? captured = null;
        db.Setup(dataStore => dataStore.AddRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, CancellationToken>((row, _) => captured = row)
            .Returns(ValueTask.CompletedTask);

        AuthTokenService sut = CreateSut(db, jwt);

        // Act
        Result<AuthTokensResultDto> result = await sut.IssueTokensAsync(new UserId(3), "a@test.example", "Alice");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("access-jwt", result.Value.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
        Assert.NotNull(captured);
        Assert.Equal(new UserId(3), captured!.UserId);
        Assert.Equal(RefreshTokenHashUtility.Hash(result.Value.RefreshToken), captured.TokenHash);
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(new UserId(3), "a@test.example", "Alice"), Times.Once);
        db.Verify(dataStore => dataStore.AddRefreshTokenAsync(captured, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.IssueTokensAsync"/> fails when a non-positive user ID is supplied.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task IssueTokensAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        // Arrange
        AuthTokenService sut = CreateSut(new Mock<IRefreshTokenRepository>(), new Mock<IJwtTokenProvider>());

        // Act
        Result<AuthTokensResultDto> result = await sut.IssueTokensAsync(new UserId(invalidUserId), "a@test.dev", "X");

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("User ID must be positive", result.Errors[0].Message);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.RotateAsync"/> rejects null, empty, or whitespace tokens
    /// without querying the repository to protect against timing attacks.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RotateAsync_Should_NotTouchDatabase_WhenPlainMissingOrWhitespace(string? blankToken)
    {
        // Arrange
        Mock<IRefreshTokenRepository> db = new();
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());

        // Act
        Result<AuthTokensResultDto> result = await sut.RotateAsync(blankToken!);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<InvalidCredentialsError>(result.Errors.First());
        db.Verify(dataStore => dataStore.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.RotateAsync"/> fails with <see cref="InvalidCredentialsError"/>
    /// when the token hash is not found in the persistence store.
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_ReturnInvalidCredentials_WhenNoRefreshMatchesHash()
    {
        // Arrange
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());

        // Act
        Result<AuthTokensResultDto> result = await sut.RotateAsync("orphan-plain");

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<InvalidCredentialsError>(result.Errors.First());
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.RotateAsync"/> fails when the loaded refresh token
    /// lacks an associated user navigation entity.
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_Abort_WhenStoredSessionHasNoUserNavigation()
    {
        // Arrange
        string plain = "token";
        string hash = RefreshTokenHashUtility.Hash(plain);
        Mock<IRefreshTokenRepository> db = new();
        RefreshToken row = RefreshToken.Create(new UserId(1), hash, DateTime.UtcNow.AddDays(1), DateTime.UtcNow);
        typeof(RefreshToken).GetProperty("User")!.SetValue(row, null);
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        Mock<IJwtTokenProvider> jwt = new();
        AuthTokenService sut = CreateSut(db, jwt);

        // Act
        Result<AuthTokensResultDto> result = await sut.RotateAsync(plain);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<InvalidCredentialsError>(result.Errors.First());
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<UserId>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.RotateAsync"/> completes successful token rotation,
    /// invalidating the previous refresh token and issuing a new JWT and refresh token pair.
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_CompleteRotation_WithNewRefreshMaterial_AndIssueJwt()
    {
        // Arrange
        string plainOld = "old-refresh-plain";
        string hashOld = RefreshTokenHashUtility.Hash(plainOld);
        RefreshToken oldRow = RefreshToken.Create(
            new UserId(7),
            hashOld,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow);
        typeof(RefreshToken).GetProperty("Id")!.SetValue(oldRow, 10);
        typeof(RefreshToken).GetProperty("User")!.SetValue(oldRow, new User { Id = new UserId(7), Email = Email.Parse("u@example.org"), Name = "Uwe" });

        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRow);
        db.Setup(dataStore => dataStore.CompleteRefreshRotationAsync(
                It.IsAny<RefreshToken>(),
                It.IsAny<RefreshToken>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock<IJwtTokenProvider> jwt = new();
        jwt.Setup(tokenIssuer => tokenIssuer.Issue(new UserId(7), "u@example.org", "Uwe"))
            .Returns(new JwtAccessTokenResultDto("new-access", DateTimeOffset.UtcNow.AddMinutes(20)));

        AuthTokenService sut = CreateSut(db, jwt);

        // Act
        Result<AuthTokensResultDto> result = await sut.RotateAsync(plainOld);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("new-access", result.Value.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));

        jwt.Verify(tokenIssuer => tokenIssuer.Issue(new UserId(7), "u@example.org", "Uwe"), Times.Once);
        db.Verify(
            dataStore => dataStore.CompleteRefreshRotationAsync(
                oldRow,
                It.Is<RefreshToken>(nr => nr.UserId == new UserId(7) && nr.TokenHash == RefreshTokenHashUtility.Hash(result.Value.RefreshToken)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.RotateAsync"/> returns <see cref="TokenCompromisedError"/>
    /// when a concurrency race occurs and <see cref="IRefreshTokenRepository.CompleteRefreshRotationAsync"/> returns false.
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_ReturnTokenCompromisedError_WhenRotationConflictOccurs()
    {
        // Arrange
        string plainOld = "concurrent-loser";
        string hashOld = RefreshTokenHashUtility.Hash(plainOld);
        RefreshToken oldRow = RefreshToken.Create(
            new UserId(7),
            hashOld,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow);
        typeof(RefreshToken).GetProperty("Id")!.SetValue(oldRow, 99);
        typeof(RefreshToken).GetProperty("User")!.SetValue(oldRow, new User { Id = new UserId(7), Email = Email.Parse("u@example.org"), Name = "Uwe" });

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

        // Act
        Result<AuthTokensResultDto> result = await sut.RotateAsync(plainOld);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<TokenCompromisedError>(result.Errors.First());
        Assert.Contains("Token rotation conflict", result.Errors.First().Message);
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<UserId>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.RotateAsync"/> detects replay attacks when an already revoked token is used,
    /// traverses the successor graph, revokes the entire token family, and fails with <see cref="TokenCompromisedError"/>.
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_DetectReplay_AndRevokeFullTokenFamilyHierarchy()
    {
        // Arrange
        var now = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider(now);

        string plainOld = "revoked-compromised-token";
        string hashOld = RefreshTokenHashUtility.Hash(plainOld);

        // Compromised Token 1 (Id 10, Replaced by 20)
        RefreshToken token1 = RefreshToken.Create(new UserId(7), hashOld, now.AddDays(7), now.AddDays(-2));
        typeof(RefreshToken).GetProperty("Id")!.SetValue(token1, 10);
        token1.Revoke(now.AddDays(-1), replacedByTokenId: 20);
        typeof(RefreshToken).GetProperty("User")!.SetValue(token1, new User { Id = new UserId(7), Email = Email.Parse("u@example.org"), Name = "Uwe" });

        // Successor Token 2 (Id 20, Replaced by 30)
        RefreshToken token2 = RefreshToken.Create(new UserId(7), "hash2", now.AddDays(7), now.AddDays(-1));
        typeof(RefreshToken).GetProperty("Id")!.SetValue(token2, 20);
        token2.Revoke(now.AddHours(-2), replacedByTokenId: 30);

        // Active Successor Token 3 (Id 30, Unrevoked)
        RefreshToken token3 = RefreshToken.Create(new UserId(7), "hash3", now.AddDays(7), now.AddHours(-2));
        typeof(RefreshToken).GetProperty("Id")!.SetValue(token3, 30);

        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token1);
        db.Setup(dataStore => dataStore.GetAllRefreshTokensForUserAsync(new UserId(7), It.IsAny<CancellationToken>()))
            .ReturnsAsync([token1, token2, token3]);
        db.Setup(dataStore => dataStore.UpdateTokensAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        Mock<ILogger<AuthTokenService>> logger = new();
        Mock<IJwtTokenProvider> jwt = new();

        AuthTokenService sut = CreateSut(db, jwt, logger, timeProvider: timeProvider);

        // Act
        Result<AuthTokensResultDto> result = await sut.RotateAsync(plainOld);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<TokenCompromisedError>(result.Errors.First());
        Assert.Contains("Token family revoked", result.Errors.First().Message);

        // Verify that active token3 in the chain is now revoked
        Assert.True(token3.IsRevoked);
        Assert.Equal(now, token3.RevokedAt);
        db.Verify(dataStore => dataStore.UpdateTokensAsync(It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
        jwt.Verify(tokenIssuer => tokenIssuer.Issue(It.IsAny<UserId>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.RotateAsync"/> fails with <see cref="InvalidCredentialsError"/>
    /// when the presented token is expired.
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_FailWithInvalidCredentials_WhenTokenExpired()
    {
        // Arrange
        var now = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider(now);

        string plainOld = "expired-refresh-plain";
        string hashOld = RefreshTokenHashUtility.Hash(plainOld);
        RefreshToken oldRow = RefreshToken.Create(
            new UserId(8),
            hashOld,
            now.AddMinutes(-10),
            now.AddDays(-15));
        typeof(RefreshToken).GetProperty("Id")!.SetValue(oldRow, 11);
        typeof(RefreshToken).GetProperty("User")!.SetValue(oldRow, new User { Id = new UserId(8), Email = Email.Parse("e@example.org"), Name = "E" });

        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetRefreshTokenByHashAsync(hashOld, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRow);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>(), timeProvider: timeProvider);

        // Act
        Result<AuthTokensResultDto> result = await sut.RotateAsync(plainOld);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<InvalidCredentialsError>(result.Errors.First());
        db.Verify(dataStore => dataStore.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.TryRevokeRefreshForUserAsync"/> fails when passed null or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TryRevokeRefreshForUserAsync_Should_Fail_WhenTokenBlank(string? blankToken)
    {
        // Arrange
        Mock<IRefreshTokenRepository> db = new();
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());

        // Act
        Result result = await sut.TryRevokeRefreshForUserAsync(blankToken!, new UserId(1));

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Refresh token cannot be empty", result.Errors.First().Message);
        db.Verify(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.TryRevokeRefreshForUserAsync"/> fails when the active token is not found.
    /// </summary>
    [Fact]
    public async Task TryRevokeRefreshForUserAsync_Should_Fail_WhenTokenNotFound()
    {
        // Arrange
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);
        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());

        // Act
        Result result = await sut.TryRevokeRefreshForUserAsync("unknown-token", new UserId(1));

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Token not found", result.Errors.First().Message);
        db.Verify(dataStore => dataStore.RevokeRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.TryRevokeRefreshForUserAsync"/> fails and avoids revocation
    /// if the active token belongs to a different user ID.
    /// </summary>
    [Fact]
    public async Task TryRevokeRefreshForUserAsync_Should_NotRevokeForeignUserSession()
    {
        // Arrange
        string plain = "secret";
        string hash = RefreshTokenHashUtility.Hash(plain);
        RefreshToken row = RefreshToken.Create(new UserId(5), hash, DateTime.UtcNow.AddDays(1), DateTime.UtcNow);
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());

        // Act
        Result result = await sut.TryRevokeRefreshForUserAsync(plain, new UserId(99));

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("does not belong to user", result.Errors.First().Message);
        db.Verify(dataStore => dataStore.RevokeRefreshTokenAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.TryRevokeRefreshForUserAsync"/> revokes the token when
    /// the token hash and authenticated user ID match.
    /// </summary>
    [Fact]
    public async Task TryRevokeRefreshForUserAsync_Should_Revoke_WhenHashMatchesAuthenticatedUser()
    {
        // Arrange
        string plain = "secret";
        string hash = RefreshTokenHashUtility.Hash(plain);
        RefreshToken row = RefreshToken.Create(new UserId(12), hash, DateTime.UtcNow.AddDays(1), DateTime.UtcNow);
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.GetActiveRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        db.Setup(dataStore => dataStore.RevokeRefreshTokenAsync(row, It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());

        // Act
        Result result = await sut.TryRevokeRefreshForUserAsync(plain, new UserId(12));

        // Assert
        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.RevokeRefreshTokenAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.RevokeAllForUserAsync"/> delegates mass session invalidation
    /// to the underlying repository.
    /// </summary>
    [Fact]
    public async Task RevokeAllForUserAsync_Should_DelegateToRepository()
    {
        // Arrange
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(dataStore => dataStore.RevokeAllRefreshTokensForUserAsync(new UserId(44), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>());

        // Act
        Result result = await sut.RevokeAllForUserAsync(new UserId(44));

        // Assert
        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.RevokeAllRefreshTokensForUserAsync(new UserId(44), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.RotateAsync"/> rejects rotation when the token's absolute session lifetime has expired.
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_Fail_WhenAbsoluteSessionLifetimeExpired()
    {
        // Arrange
        DateTime nowUtc = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        FakeTimeProvider timeProvider = new(new DateTimeOffset(nowUtc));

        string plain = "active-token";
        string hash = RefreshTokenHashUtility.Hash(plain);
        User user = new() { Id = new UserId(1), Email = Email.Parse("user@test.com"), Name = "User" };

        // Sliding ExpiresAt is in the future, but AbsoluteExpiresAt is in the past
        RefreshToken old = RefreshToken.Create(
            user.Id,
            hash,
            expiresAt: nowUtc.AddDays(5),
            createdAt: nowUtc.AddDays(-35),
            absoluteExpiresAt: nowUtc.AddDays(-1));
        typeof(RefreshToken).GetProperty(nameof(RefreshToken.User))!.SetValue(old, user);

        Mock<IRefreshTokenRepository> db = new();
        db.Setup(repo => repo.GetRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(old);

        AuthTokenService sut = CreateSut(db, new Mock<IJwtTokenProvider>(), timeProvider: timeProvider);

        // Act
        Result<AuthTokensResultDto> result = await sut.RotateAsync(plain);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<InvalidCredentialsError>(result.Errors[0]);
    }

    /// <summary>
    /// Tests that <see cref="AuthTokenService.RotateAsync"/> caps the new token's ExpiresAt timestamp so it never exceeds AbsoluteExpiresAt.
    /// </summary>
    [Fact]
    public async Task RotateAsync_Should_CapNewExpiresAt_ToAbsoluteExpiresAt()
    {
        // Arrange
        DateTime nowUtc = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        FakeTimeProvider timeProvider = new(new DateTimeOffset(nowUtc));

        string plain = "active-token";
        string hash = RefreshTokenHashUtility.Hash(plain);
        User user = new() { Id = new UserId(1), Email = Email.Parse("user@test.com"), Name = "User" };

        DateTime absoluteCap = nowUtc.AddDays(3); // Less than the 14-day sliding window
        RefreshToken old = RefreshToken.Create(
            user.Id,
            hash,
            expiresAt: nowUtc.AddDays(14),
            createdAt: nowUtc.AddDays(-27),
            absoluteExpiresAt: absoluteCap);
        typeof(RefreshToken).GetProperty(nameof(RefreshToken.User))!.SetValue(old, user);

        RefreshToken? capturedNew = null;
        Mock<IRefreshTokenRepository> db = new();
        db.Setup(repo => repo.GetRefreshTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(old);
        db.Setup(repo => repo.CompleteRefreshRotationAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Callback<RefreshToken, RefreshToken, CancellationToken>((_, n, _) => capturedNew = n)
            .ReturnsAsync(true);

        Mock<IJwtTokenProvider> jwt = new();
        jwt.Setup(j => j.Issue(user.Id, user.Email.Value, user.Name))
            .Returns(new JwtAccessTokenResultDto("new-jwt", nowUtc.AddMinutes(15)));

        AuthTokenService sut = CreateSut(db, jwt, timeProvider: timeProvider);

        // Act
        Result<AuthTokensResultDto> result = await sut.RotateAsync(plain);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedNew);
        Assert.Equal(absoluteCap, capturedNew!.ExpiresAt);
        Assert.Equal(absoluteCap, capturedNew.AbsoluteExpiresAt);
    }
}
