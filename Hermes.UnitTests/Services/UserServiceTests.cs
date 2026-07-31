using Hermes.Application.DTOs.User;
using Hermes.Application.DTOs.Login;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Scheduling;
using Hermes.Application.Security;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class UserServiceTests
{
    private static UserService CreateUserService(
        IUserRepository db,
        IVerificationMailJobTrigger? trigger = null,
        bool hashEmailVerificationCodes = true) =>
        new(
            db,
            trigger ?? Mock.Of<IVerificationMailJobTrigger>(),
            Options.Create(new SecurityOptions { HashEmailVerificationCodes = hashEmailVerificationCodes }));

    [Fact]
    public async Task RegisterUserAsync_Should_NormalizeEmail_AndStoreOnlyBcryptHashOfPassword()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = 100)
            .Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        RegisterUserRequestDto user = new()
        {
            Name = "Tester",
            Email = "  Hello@Test.COM ",
            Password = "plain-secret",
        };
        UserScopeDto scope = await sut.RegisterUserAsync(user);
        Assert.Equal("hello@test.com", scope.Email);
        db.Verify(dataStore => dataStore.SetUserAsync(
            It.Is<User>(registeredUser => BCrypt.Net.BCrypt.Verify("plain-secret", registeredUser.PasswordHash)),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(100, scope.UserId);
    }

    [Fact]
    public async Task RegisterUserAsync_Should_RejectWhitespaceOnlyDisplayName()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = 5)
            .Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        RegisterUserRequestDto user = new() { Name = "   ", Email = "ok@test.dev", Password = "pw" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RegisterUserAsync(user));
        db.Verify(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterUserAsync_Should_Fail_WhenDatabaseLeavesIdAtZero()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        RegisterUserRequestDto user = new() { Name = "A", Email = "a@b.c", Password = "x" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RegisterUserAsync(user));
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenIdentifierBlank()
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());

        LoginResultDto loginResult = await sut.LoginAsync("   ", "pw");

        Assert.False(loginResult.Success);
        Assert.Contains("required", loginResult.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Blank password yields a generic failure (no enumeration).</summary>
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenPasswordBlank()
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());

        LoginResultDto loginResult = await sut.LoginAsync("user", "");

        Assert.False(loginResult.Success);
        Assert.False(string.IsNullOrEmpty(loginResult.ErrorMessage));
    }

    [Fact]
    public async Task LoginAsync_Should_LookupByEmail_WhenIdentifierContainsAtSign()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("good");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync("me@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 3, Email = "me@test.dev", PasswordHash = hash, Name = "Me" });

        UserService sut = CreateUserService(db.Object);

        LoginResultDto loginResult = await sut.LoginAsync(" me@test.dev ", "good");

        Assert.True(loginResult.Success);
        Assert.Equal(3, loginResult.UserId);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_Should_LookupByName_WhenIdentifierHasNoAtSign()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("pw");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 2, Email = "a@b.c", PasswordHash = hash, Name = "alice" });

        UserService sut = CreateUserService(db.Object);

        LoginResultDto loginResult = await sut.LoginAsync("alice", "pw");

        Assert.True(loginResult.Success);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Known user + wrong secret uses the same message as unknown user.</summary>
    [Fact]
    public async Task LoginAsync_Should_NotRevealWhetherAccountExists_OnFailure()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("right");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, PasswordHash = hash, Name = "bob", Email = "b@c.d" });

        UserService sut = CreateUserService(db.Object);

        LoginResultDto loginResult = await sut.LoginAsync("bob", "wrong");

        Assert.False(loginResult.Success);
        Assert.Equal("Invalid login or password.", loginResult.ErrorMessage);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_HashNewPassword_WhenCurrentPasswordVerified()
    {
        string existingHash = BCrypt.Net.BCrypt.HashPassword("oldpw");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, Email = "x@y.z", Name = "X", PasswordHash = existingHash });
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 5, Email = "x@y.z", Name = "X", PasswordHash = "new-secret" };

        await sut.UpdateUserAsync(patch, currentPasswordPlain: "oldpw");

        Assert.True(BCrypt.Net.BCrypt.Verify("new-secret", patch.PasswordHash));
        db.Verify(dataStore => dataStore.UpdateUserAsync(patch, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_RequireCurrentPassword_WhenChangingPassword()
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());
        User patch = new() { Id = 1, Email = "a@b.c", Name = "N", PasswordHash = "new" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: null));
    }

    [Fact]
    public async Task UpdateUserAsync_Should_RejectWrongCurrentPassword()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 9, Email = "e@f.g", Name = "E", PasswordHash = BCrypt.Net.BCrypt.HashPassword("real") });

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 9, Email = "e@f.g", Name = "E", PasswordHash = "hacker" };

        await Assert.ThrowsAsync<WrongCurrentPasswordException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "wrong-old"));
    }

    [Fact]
    public async Task UpdateUserAsync_Should_ThrowUserNotFound_WhenChangingPassword_AndUserMissing()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 404, Email = "a@b.c", Name = "N", PasswordHash = "new-Valid_9!" };

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "old"));
    }

    [Fact]
    public async Task UpdateUserAsync_Should_ThrowInvalidOperation_WhenStoredPasswordHashEmpty()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "a@b.c", Name = "N", PasswordHash = null });

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 1, Email = "a@b.c", Name = "N", PasswordHash = "new-Valid_9!" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "anything"));
    }

    /// <summary>Skipping password change must avoid loading stored hash for verification.</summary>
    [Fact]
    public async Task UpdateUserAsync_Should_UpdateWithoutPassword_WhenNewPasswordOmitted()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 2, Email = "u@x.y", Name = "OnlyName", PasswordHash = null };

        await sut.UpdateUserAsync(patch, currentPasswordPlain: null);

        Assert.Null(patch.PasswordHash);
        db.Verify(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(dataStore => dataStore.UpdateUserAsync(patch, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserByNameAsync_Should_ReturnScope_FromStore()
    {
        UserScopeDto expected = new() { UserId = 7, Name = "Sam", Email = "sam@test.dev" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserByNameAsync("sam", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        UserService sut = CreateUserService(db.Object);

        UserScopeDto? r = await sut.GetUserByNameAsync("sam");

        Assert.Same(expected, r);
    }

    [Fact]
    public async Task GetUserByNameAsync_Should_RejectBlankName()
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByNameAsync("  "));
    }

    [Fact]
    public async Task GetUserByIdAsync_Should_ReturnScope_FromStore()
    {
        UserScopeDto expected = new() { UserId = 3, Email = "e@e.e", Name = "E" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        UserService sut = CreateUserService(db.Object);
        UserScopeDto? r = await sut.GetUserByIdAsync(3);

        Assert.Same(expected, r);
    }

    [Fact]
    public async Task GetUserByEmailAsync_Should_ReturnScope_FromStore_WhenNormalized()
    {
        UserScopeDto expected = new() { UserId = 9, Email = "a@b.c", Name = "A" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserByEmailAsync("a@b.c", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        UserService sut = CreateUserService(db.Object);
        UserScopeDto? r = await sut.GetUserByEmailAsync("a@b.c");

        Assert.Same(expected, r);
    }

    [Fact]
    public async Task SendVerificationMailAsync_Should_EnqueueJob_WhenUserExists()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync("u@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 42, Email = "u@test.dev" });

        Mock<IVerificationMailJobTrigger> trigger = new();
        trigger.Setup(jobTrigger => jobTrigger.EnqueueSendVerificationMail(42)).Returns("job-1");

        UserService sut = CreateUserService(db.Object, trigger.Object);

        await sut.SendVerificationMailAsync("  U@Test.dev ", CancellationToken.None);

        trigger.Verify(jobTrigger => jobTrigger.EnqueueSendVerificationMail(42), Times.Once);
    }

    [Fact]
    public async Task SendVerificationMailAsync_Should_ThrowUserNotFound_WhenEmailUnknown()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserService sut = CreateUserService(db.Object);

        await Assert.ThrowsAsync<UserNotFoundException>(() => sut.SendVerificationMailAsync("ghost@test.dev", CancellationToken.None));
    }

    [Fact]
    public async Task SendVerificationMailAsync_Should_RejectBlankEmail()
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SendVerificationMailAsync("  ", CancellationToken.None));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public async Task CheckVerificationCodeAsync_Should_RejectInvalidCode(int invalidCode)
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.CheckVerificationCodeAsync(1, invalidCode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CheckVerificationCodeAsync_Should_RejectInvalidUserId(int invalidUserId)
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.CheckVerificationCodeAsync(invalidUserId, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowUserNotFound_WhenUserMissing()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserService sut = CreateUserService(db.Object);

        await Assert.ThrowsAsync<UserNotFoundException>(() => sut.CheckVerificationCodeAsync(5, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenNoChallengeStored()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, TwoFactorCode = null, TwoFactorExpiry = null });

        UserService sut = CreateUserService(db.Object);

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
                TwoFactorCode = RefreshTokenHasher.Hash("123456"),
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(-5),
            });

        UserService sut = CreateUserService(db.Object);

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
                TwoFactorCode = RefreshTokenHasher.Hash("999999"),
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(10),
            });

        UserService sut = CreateUserService(db.Object);

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
                TwoFactorCode = RefreshTokenHasher.Hash("123456"),
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });
        db.Setup(dataStore => dataStore.CompleteUserEmailVerificationAsync(8, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);

        await sut.CheckVerificationCodeAsync(8, 123456);

        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(8, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Gradual rollout: hashed-check path still accepts legacy plaintext challenges.</summary>
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

        UserService sut = CreateUserService(db.Object, trigger: null, hashEmailVerificationCodes: true);

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

        UserService sut = CreateUserService(db.Object, trigger: null, hashEmailVerificationCodes: false);

        await sut.CheckVerificationCodeAsync(3, 654321);

        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(3, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetUserByIdAsync_Should_RejectNonPositiveId(int invalidId)
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByIdAsync(invalidId));
    }

    [Fact]
    public async Task GetUserByEmailAsync_Should_RejectBlankEmail()
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByEmailAsync("  "));
    }

    [Fact]
    public async Task DeleteUserAsync_Should_DelegateToStore_WhenScopeValid()
    {
        Mock<IUserRepository> db = new();
        UserScopeDto scope = new() { UserId = 1, Email = "a@b", Name = "A" };
        db.Setup(dataStore => dataStore.DeleteUserAsync(scope, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        await sut.DeleteUserAsync(scope);

        db.Verify(dataStore => dataStore.DeleteUserAsync(scope, It.IsAny<CancellationToken>()), Times.Once);
    }
}
