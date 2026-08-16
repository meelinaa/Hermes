using FluentResults;
using Hermes.Application.Errors;
using Hermes.Application.Options.Auth;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Security;
using Hermes.Application.Services.Users;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Contains unit tests for <see cref="UserVerificationService"/>,
/// testing two-factor challenge dispatching, numeric OTP validation boundaries,
/// constant-time verification comparisons, and hash format fallbacks with FluentResults.
/// </summary>
public sealed class UserVerificationServiceTests
{
    private static UserVerificationService CreateService(
        IUserRepository db,
        IVerificationMailJobService? trigger = null,
        bool hashEmailVerificationCodes = true,
        TimeProvider? timeProvider = null) =>
        new(
            db,
            db,
            trigger ?? Mock.Of<IVerificationMailJobService>(),
            Options.Create(new SecurityOptions { HashEmailVerificationCodes = hashEmailVerificationCodes }),
            timeProvider ?? TimeProvider.System);

    /// <summary>
    /// Tests that <see cref="UserVerificationService.SendVerificationMailAsync"/> enqueues a background job
    /// to send a verification email when the user exists for the given normalized address.
    /// </summary>
    [Fact]
    public async Task SendVerificationMailAsync_Should_EnqueueJob_WhenUserExists()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync("u@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(42), Email = Email.Parse("u@test.dev") });

        Mock<IVerificationMailJobService> trigger = new();
        trigger.Setup(jobTrigger => jobTrigger.EnqueueSendVerificationMail(new UserId(42))).Returns("job-1");

        UserVerificationService sut = CreateService(db.Object, trigger.Object);

        // Act
        Result result = await sut.SendVerificationMailAsync("  U@Test.dev ", CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        trigger.Verify(jobTrigger => jobTrigger.EnqueueSendVerificationMail(new UserId(42)), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="UserVerificationService.SendVerificationMailAsync"/> returns a <see cref="UserNotFoundError"/>
    /// when the requested email address is not registered in the system.
    /// </summary>
    [Fact]
    public async Task SendVerificationMailAsync_Should_ReturnUserNotFound_WhenEmailUnknown()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserVerificationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.SendVerificationMailAsync("ghost@test.dev", CancellationToken.None);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<UserNotFoundError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserVerificationService.SendVerificationMailAsync"/> returns a <see cref="ValidationError"/>
    /// when the provided email string is null, empty, or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendVerificationMailAsync_Should_RejectBlankEmail(string? blankEmail)
    {
        // Arrange
        UserVerificationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act
        Result result = await sut.SendVerificationMailAsync(blankEmail!, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<ValidationError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserVerificationService.CheckVerificationCodeAsync"/> rejects codes outside the 6-digit boundary [0, 999999].
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    [InlineData(1_234_567)]
    public async Task CheckVerificationCodeAsync_Should_RejectInvalidCode(int invalidCode)
    {
        // Arrange
        UserVerificationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act
        Result result = await sut.CheckVerificationCodeAsync(new UserId(1), invalidCode);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<ValidationError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserVerificationService.CheckVerificationCodeAsync"/> rejects non-positive user identifiers.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CheckVerificationCodeAsync_Should_RejectInvalidUserId(int invalidUserId)
    {
        // Arrange
        UserVerificationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act
        Result result = await sut.CheckVerificationCodeAsync(new UserId(invalidUserId), 123456);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<ValidationError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserVerificationService.CheckVerificationCodeAsync"/> returns <see cref="UserNotFoundError"/>
    /// when the user record does not exist in the database.
    /// </summary>
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ReturnUserNotFound_WhenUserMissing()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserVerificationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.CheckVerificationCodeAsync(new UserId(5), 123456);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<UserNotFoundError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserVerificationService.CheckVerificationCodeAsync"/> returns <see cref="VerificationCodeMismatchError"/>
    /// when the user has no active challenge code or expiration timestamp stored.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("123456", null)]
    [InlineData(null, "2026-08-16T12:00:00Z")]
    [InlineData("   ", "2026-08-16T12:00:00Z")]
    public async Task CheckVerificationCodeAsync_Should_ReturnVerificationCodeMismatch_WhenNoChallengeStored(string? code, string? expiryIso)
    {
        // Arrange
        DateTime? expiry = expiryIso != null ? DateTime.Parse(expiryIso) : null;
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), TwoFactorCode = code, TwoFactorExpiry = expiry });

        UserVerificationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.CheckVerificationCodeAsync(new UserId(1), 123456);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<VerificationCodeMismatchError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserVerificationService.CheckVerificationCodeAsync"/> returns <see cref="VerificationCodeMismatchError"/>
    /// when the challenge code has expired (tested with FakeTimeProvider and Unspecified DateTimeKind).
    /// </summary>
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ReturnVerificationCodeMismatch_WhenExpired()
    {
        // Arrange
        var now = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider(now);

        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = new UserId(1),
                TwoFactorCode = RefreshTokenHashUtility.Hash("123456"),
                TwoFactorExpiry = DateTime.SpecifyKind(now.AddSeconds(-1), DateTimeKind.Unspecified),
            });

        UserVerificationService sut = CreateService(db.Object, timeProvider: timeProvider);

        // Act
        Result result = await sut.CheckVerificationCodeAsync(new UserId(1), 123456);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<VerificationCodeMismatchError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserVerificationService.CheckVerificationCodeAsync"/> returns <see cref="VerificationCodeMismatchError"/>
    /// when the code does not match the stored hash.
    /// </summary>
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ReturnVerificationCodeMismatch_WhenCodeWrong()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = new UserId(1),
                TwoFactorCode = RefreshTokenHashUtility.Hash("999999"),
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(10),
            });

        UserVerificationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.CheckVerificationCodeAsync(new UserId(1), 123456);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<VerificationCodeMismatchError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserVerificationService.CheckVerificationCodeAsync"/> completes verification
    /// when the code (formatted with leading zeros) matches the hashed challenge.
    /// </summary>
    [Theory]
    [InlineData(0, "000000")]
    [InlineData(7, "000007")]
    [InlineData(123456, "123456")]
    [InlineData(999999, "999999")]
    public async Task CheckVerificationCodeAsync_Should_CompleteVerification_WhenCodeAndExpiryValid(int code, string formattedCode)
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(8), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = new UserId(8),
                TwoFactorCode = RefreshTokenHashUtility.Hash(formattedCode),
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });
        db.Setup(dataStore => dataStore.CompleteUserEmailVerificationAsync(new UserId(8), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        UserVerificationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.CheckVerificationCodeAsync(new UserId(8), code);

        // Assert
        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(new UserId(8), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests backward compatibility where a legacy unhashed 6-digit code stored in the database
    /// is accepted even when hashing is enabled.
    /// </summary>
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_AcceptLegacyPlaintext_WhenHashingEnabled_ButStoredChallengeIsPlainSixDigits()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = new UserId(2),
                TwoFactorCode = "123456",
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });
        db.Setup(dataStore => dataStore.CompleteUserEmailVerificationAsync(new UserId(2), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        UserVerificationService sut = CreateService(db.Object, trigger: null, hashEmailVerificationCodes: true);

        // Act
        Result result = await sut.CheckVerificationCodeAsync(new UserId(2), 123456);

        // Assert
        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(new UserId(2), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that a 64-character stored code containing invalid non-hex characters (e.g. 'G', 'Z', lowercase 'a')
    /// is not treated as a valid SHA-256 hash and falls back to plaintext comparison.
    /// </summary>
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_FallbackToPlaintext_When64CharStringContainsNonUpperHexDigits()
    {
        // Arrange
        string invalidHex64 = new string('A', 63) + "Z"; // 'Z' is not valid hex
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(99), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = new UserId(99),
                TwoFactorCode = invalidHex64,
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });

        UserVerificationService sut = CreateService(db.Object, trigger: null, hashEmailVerificationCodes: true);

        // Act
        Result result = await sut.CheckVerificationCodeAsync(new UserId(99), 123456);

        // Assert (Since provided 6-digit string does not equal the 64-char string, it must fail)
        Assert.True(result.IsFailed);
        Assert.IsType<VerificationCodeMismatchError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that verification succeeds with plaintext storage when hashing is disabled in configuration.
    /// </summary>
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_UsePlaintextPersisted_WhenHashingDisabled()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(3), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = new UserId(3),
                TwoFactorCode = "654321",
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });
        db.Setup(dataStore => dataStore.CompleteUserEmailVerificationAsync(new UserId(3), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        UserVerificationService sut = CreateService(db.Object, trigger: null, hashEmailVerificationCodes: false);

        // Act
        Result result = await sut.CheckVerificationCodeAsync(new UserId(3), 654321);

        // Assert
        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(new UserId(3), It.IsAny<CancellationToken>()), Times.Once);
    }
}

