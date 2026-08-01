using Hermes.Application.Options;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Scheduling;
using Hermes.Application.Security;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
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
            Options.Create(new SecurityOptions { HashEmailVerificationCodes = hashEmailVerificationCodes }));

    [Fact]
    public async Task SendVerificationMailAsync_Should_EnqueueJob_WhenUserExists()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync("u@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 42, Email = "u@test.dev" });

        Mock<IVerificationMailJobService> trigger = new();
        trigger.Setup(jobTrigger => jobTrigger.EnqueueSendVerificationMail(42)).Returns("job-1");

        UserVerificationService sut = CreateService(db.Object, trigger.Object);

        await sut.SendVerificationMailAsync("  U@Test.dev ", CancellationToken.None);

        trigger.Verify(jobTrigger => jobTrigger.EnqueueSendVerificationMail(42), Times.Once);
    }

    [Fact]
    public async Task SendVerificationMailAsync_Should_ThrowUserNotFound_WhenEmailUnknown()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserVerificationService sut = CreateService(db.Object);

        await Assert.ThrowsAsync<UserNotFoundException>(() => sut.SendVerificationMailAsync("ghost@test.dev", CancellationToken.None));
    }

    [Fact]
    public async Task SendVerificationMailAsync_Should_RejectBlankEmail()
    {
        UserVerificationService sut = CreateService(Mock.Of<IUserRepository>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SendVerificationMailAsync("  ", CancellationToken.None));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public async Task CheckVerificationCodeAsync_Should_RejectInvalidCode(int invalidCode)
    {
        UserVerificationService sut = CreateService(Mock.Of<IUserRepository>());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.CheckVerificationCodeAsync(1, invalidCode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CheckVerificationCodeAsync_Should_RejectInvalidUserId(int invalidUserId)
    {
        UserVerificationService sut = CreateService(Mock.Of<IUserRepository>());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.CheckVerificationCodeAsync(invalidUserId, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowUserNotFound_WhenUserMissing()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserVerificationService sut = CreateService(db.Object);

        await Assert.ThrowsAsync<UserNotFoundException>(() => sut.CheckVerificationCodeAsync(5, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenNoChallengeStored()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, TwoFactorCode = null, TwoFactorExpiry = null });

        UserVerificationService sut = CreateService(db.Object);

        await Assert.ThrowsAsync<VerificationCodeMismatchException>(() => sut.CheckVerificationCodeAsync(1, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenExpired()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = 1,
                TwoFactorCode = RefreshTokenHashService.Hash("123456"),
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(-5),
            });

        UserVerificationService sut = CreateService(db.Object);

        await Assert.ThrowsAsync<VerificationCodeMismatchException>(() => sut.CheckVerificationCodeAsync(1, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenCodeWrong()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = 1,
                TwoFactorCode = RefreshTokenHashService.Hash("999999"),
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(10),
            });

        UserVerificationService sut = CreateService(db.Object);

        await Assert.ThrowsAsync<VerificationCodeMismatchException>(() => sut.CheckVerificationCodeAsync(1, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_CompleteVerification_WhenCodeAndExpiryValid()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = 8,
                TwoFactorCode = RefreshTokenHashService.Hash("123456"),
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });
        db.Setup(dataStore => dataStore.CompleteUserEmailVerificationAsync(8, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserVerificationService sut = CreateService(db.Object);

        await sut.CheckVerificationCodeAsync(8, 123456);

        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(8, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_AcceptLegacyPlaintext_WhenHashingEnabled_ButStoredChallengeIsPlainSixDigits()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = 2,
                TwoFactorCode = "123456",
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });
        db.Setup(dataStore => dataStore.CompleteUserEmailVerificationAsync(2, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserVerificationService sut = CreateService(db.Object, trigger: null, hashEmailVerificationCodes: true);

        await sut.CheckVerificationCodeAsync(2, 123456);

        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_UsePlaintextPersisted_WhenHashingDisabled()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = 3,
                TwoFactorCode = "654321",
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });
        db.Setup(dataStore => dataStore.CompleteUserEmailVerificationAsync(3, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserVerificationService sut = CreateService(db.Object, trigger: null, hashEmailVerificationCodes: false);

        await sut.CheckVerificationCodeAsync(3, 654321);

        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(3, It.IsAny<CancellationToken>()), Times.Once);
    }
}
