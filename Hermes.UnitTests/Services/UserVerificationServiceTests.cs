using Hermes.Application.Options.Auth;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Security;
using Hermes.Application.Services.Users;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class UserVerificationServiceTests
{
    private static UserVerificationService CreateService(
        IUserRepository db,
        IVerificationMailJobService? trigger = null,
        bool hashEmailVerificationCodes = true) =>
        new(
            db,
            trigger ?? Mock.Of<IVerificationMailJobService>(),
            Options.Create(new SecurityOptions { HashEmailVerificationCodes = hashEmailVerificationCodes }),
            TimeProvider.System);

    // [R]IGHT: Enqueues background job to send verification email for existing user
    [Fact]
    public async Task SendVerificationMailAsync_Should_EnqueueJob_WhenUserExists()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync("u@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(42), Email = "u@test.dev" });

        Mock<IVerificationMailJobService> trigger = new();
        trigger.Setup(jobTrigger => jobTrigger.EnqueueSendVerificationMail(new UserId(42))).Returns("job-1");

        UserVerificationService sut = CreateService(db.Object, trigger.Object);

        // Act
        await sut.SendVerificationMailAsync("  U@Test.dev ", CancellationToken.None);

        // Assert
        trigger.Verify(jobTrigger => jobTrigger.EnqueueSendVerificationMail(new UserId(42)), Times.Once);
    }

    // [E]RROR: Throws UserNotFoundException when email address is not found
    [Fact]
    public async Task SendVerificationMailAsync_Should_ThrowUserNotFound_WhenEmailUnknown()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserVerificationService sut = CreateService(db.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(async () => await sut.SendVerificationMailAsync("ghost@test.dev", CancellationToken.None));
    }

    // [B]OUNDARY: Rejects empty or whitespace email address input
    [Fact]
    public async Task SendVerificationMailAsync_Should_RejectBlankEmail()
    {
        // Arrange
        UserVerificationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => await sut.SendVerificationMailAsync("  ", CancellationToken.None));
    }

    // [B]OUNDARY: Rejects numeric verification code outside valid 6-digit range (0 - 999,999)
    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public async Task CheckVerificationCodeAsync_Should_RejectInvalidCode(int invalidCode)
    {
        // Arrange
        UserVerificationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.CheckVerificationCodeAsync(new UserId(1), invalidCode));
    }

    // [B]OUNDARY: Rejects non-positive user ID inputs
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CheckVerificationCodeAsync_Should_RejectInvalidUserId(int invalidUserId)
    {
        // Arrange
        UserVerificationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.CheckVerificationCodeAsync(new UserId(invalidUserId), 123456));
    }

    // [E]RROR: Throws UserNotFoundException when user record is missing during code verification
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowUserNotFound_WhenUserMissing()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserVerificationService sut = CreateService(db.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(async () => await sut.CheckVerificationCodeAsync(new UserId(5), 123456));
    }

    // [E]RROR: Throws VerificationCodeMismatchException when user has no active verification challenge stored
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenNoChallengeStored()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), TwoFactorCode = null, TwoFactorExpiry = null });

        UserVerificationService sut = CreateService(db.Object);

        // Act & Assert
        await Assert.ThrowsAsync<VerificationCodeMismatchException>(async () => await sut.CheckVerificationCodeAsync(new UserId(1), 123456));
    }

    // [E]RROR: Throws VerificationCodeMismatchException when stored challenge has expired
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenExpired()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = new UserId(1),
                TwoFactorCode = RefreshTokenHashUtility.Hash("123456"),
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(-5),
            });

        UserVerificationService sut = CreateService(db.Object);

        // Act & Assert
        await Assert.ThrowsAsync<VerificationCodeMismatchException>(async () => await sut.CheckVerificationCodeAsync(new UserId(1), 123456));
    }

    // [E]RROR: Throws VerificationCodeMismatchException when provided code does not match stored challenge
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenCodeWrong()
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

        // Act & Assert
        await Assert.ThrowsAsync<VerificationCodeMismatchException>(async () => await sut.CheckVerificationCodeAsync(new UserId(1), 123456));
    }

    // [R]IGHT: Completes email verification when code matches hashed challenge and is not expired
    [Fact]
    public async Task CheckVerificationCodeAsync_Should_CompleteVerification_WhenCodeAndExpiryValid()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(new UserId(8), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = new UserId(8),
                TwoFactorCode = RefreshTokenHashUtility.Hash("123456"),
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });
        db.Setup(dataStore => dataStore.CompleteUserEmailVerificationAsync(new UserId(8), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        UserVerificationService sut = CreateService(db.Object);

        // Act
        await sut.CheckVerificationCodeAsync(new UserId(8), 123456);

        // Assert
        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(new UserId(8), It.IsAny<CancellationToken>()), Times.Once);
    }

    // [R]IGHT: Backward compatibility fallback accepts legacy unhashed 6-digit challenge codes
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
        await sut.CheckVerificationCodeAsync(new UserId(2), 123456);

        // Assert
        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(new UserId(2), It.IsAny<CancellationToken>()), Times.Once);
    }

    // [R]IGHT: Verifies plaintext code when code hashing option is disabled in configuration
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
        await sut.CheckVerificationCodeAsync(new UserId(3), 654321);

        // Assert
        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(new UserId(3), It.IsAny<CancellationToken>()), Times.Once);
    }
}
