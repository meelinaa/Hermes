using Hermes.Application.Models.Login;
using Hermes.Application.Ports;
using Hermes.Application.Scheduling;
using Hermes.Application.Services;
using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Specifications for user registration and authentication: passwords hashed with BCrypt; normalized email;
/// login messages avoid account enumeration; profile updates require verified current password when changing password.
/// </summary>
public sealed class UserServiceTests
{
    private static UserService CreateUserService(IHermesDataStore db, IVerificationMailJobTrigger? trigger = null) =>
        new(db, trigger ?? Mock.Of<IVerificationMailJobTrigger>());

    /// <summary>
    /// Registration trims/normalizes email to lowercase, hashes plaintext password with BCrypt, assigns id from store callback.
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_Should_NormalizeEmail_AndStoreOnlyBcryptHashOfPassword()
    {
        // Arrange
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = 100)
            .Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        RegisterUserRequest user = new()
        {
            Name = "Tester",
            Email = "  Hello@Test.COM ",
            Password = "plain-secret",
        };

        // Act
        UserScope scope = await sut.RegisterUserAsync(user);

        // Assert
        Assert.Equal("hello@test.com", scope.Email);
        db.Verify(dataStore => dataStore.SetUserAsync(
            It.Is<User>(registeredUser => BCrypt.Net.BCrypt.Verify("plain-secret", registeredUser.PasswordHash)),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(100, scope.UserId);
    }

    /// <summary>
    /// Display name cannot be whitespace-only; store must never be called (validation before persistence).
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_Should_RejectWhitespaceOnlyDisplayName()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = 5)
            .Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        RegisterUserRequest user = new() { Name = "   ", Email = "ok@test.dev", Password = "pw" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RegisterUserAsync(user));
        db.Verify(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// If SetUserAsync does not assign a positive id, registration fails (contract with persistence layer).
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_Should_Fail_WhenDatabaseLeavesIdAtZero()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        RegisterUserRequest user = new() { Name = "A", Email = "a@b.c", Password = "x" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RegisterUserAsync(user));
    }

    /// <summary>
    /// Blank identifier fails fast without querying the database.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenIdentifierBlank()
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());

        LoginResult loginResult = await sut.LoginAsync("   ", "pw");

        Assert.False(loginResult.Success);
        Assert.Contains("required", loginResult.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Blank password fails without revealing whether the account exists.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenPasswordBlank()
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());

        LoginResult loginResult = await sut.LoginAsync("user", "");

        Assert.False(loginResult.Success);
        Assert.False(string.IsNullOrEmpty(loginResult.ErrorMessage));
    }

    /// <summary>
    /// Identifier containing '@' is treated as email lookup (normalized trim).
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_LookupByEmail_WhenIdentifierContainsAt()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("good");
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync("me@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 3, Email = "me@test.dev", PasswordHash = hash, Name = "Me" });

        UserService sut = CreateUserService(db.Object);

        LoginResult loginResult = await sut.LoginAsync(" me@test.dev ", "good");

        Assert.True(loginResult.Success);
        Assert.Equal(3, loginResult.UserId);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Identifier without '@' uses display-name lookup path.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_LookupByName_WhenIdentifierHasNoAtSign()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("pw");
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 2, Email = "a@b.c", PasswordHash = hash, Name = "alice" });

        UserService sut = CreateUserService(db.Object);

        LoginResult loginResult = await sut.LoginAsync("alice", "pw");

        Assert.True(loginResult.Success);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Wrong password yields generic error message (no distinction from unknown user).
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_NotRevealWhetherAccountExists_OnFailure()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("right");
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, PasswordHash = hash, Name = "bob", Email = "b@c.d" });

        UserService sut = CreateUserService(db.Object);

        LoginResult loginResult = await sut.LoginAsync("bob", "wrong");

        Assert.False(loginResult.Success);
        Assert.Equal("Invalid login or password.", loginResult.ErrorMessage);
    }

    /// <summary>
    /// Password change hashes new secret after verifying current password against BCrypt hash.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_HashNewPassword_WhenCurrentPasswordVerified()
    {
        string existingHash = BCrypt.Net.BCrypt.HashPassword("oldpw");
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, Email = "x@y.z", Name = "X", PasswordHash = existingHash });
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 5, Email = "x@y.z", Name = "X", PasswordHash = "new-secret" };

        await sut.UpdateUserAsync(patch, currentPasswordPlain: "oldpw");

        Assert.True(BCrypt.Net.BCrypt.Verify("new-secret", patch.PasswordHash));
        db.Verify(dataStore => dataStore.UpdateUserAsync(patch, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Changing password requires supplying current password (cannot be null).
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_RequireCurrentPassword_WhenChangingPassword()
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());
        User patch = new() { Id = 1, Email = "a@b.c", Name = "N", PasswordHash = "new" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: null));
    }

    /// <summary>
    /// Wrong current password yields domain-specific exception before persisting.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_RejectWrongCurrentPassword()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 9, Email = "e@f.g", Name = "E", PasswordHash = BCrypt.Net.BCrypt.HashPassword("real") });

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 9, Email = "e@f.g", Name = "E", PasswordHash = "hacker" };

        await Assert.ThrowsAsync<WrongCurrentPasswordException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "wrong-old"));
    }

    /// <summary>When changing password, missing user row yields <see cref="UserNotFoundException"/>.</summary>
    [Fact]
    public async Task UpdateUserAsync_Should_ThrowUserNotFound_WhenChangingPassword_AndUserMissing()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(404, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 404, Email = "a@b.c", Name = "N", PasswordHash = "new-Valid_9!" };

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "old"));
    }

    /// <summary>Cannot set new password if stored hash is missing (account without password).</summary>
    [Fact]
    public async Task UpdateUserAsync_Should_ThrowInvalidOperation_WhenStoredPasswordHashEmpty()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "a@b.c", Name = "N", PasswordHash = null });

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 1, Email = "a@b.c", Name = "N", PasswordHash = "new-Valid_9!" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "anything"));
    }

    /// <summary>Profile update without new password must not load entity for password verification.</summary>
    [Fact]
    public async Task UpdateUserAsync_Should_UpdateWithoutPassword_WhenNewPasswordOmitted()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 2, Email = "u@x.y", Name = "OnlyName", PasswordHash = null };

        await sut.UpdateUserAsync(patch, currentPasswordPlain: null);

        Assert.Null(patch.PasswordHash);
        db.Verify(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(dataStore => dataStore.UpdateUserAsync(patch, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Name lookup delegates to store after trimming validation.</summary>
    [Fact]
    public async Task GetUserByNameAsync_Should_ReturnScope_FromStore()
    {
        UserScope expected = new() { UserId = 7, Name = "Sam", Email = "sam@test.dev" };
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserByNameAsync("sam", It.IsAny<CancellationToken>())).ReturnsAsync(expected); 

        UserService sut = CreateUserService(db.Object);

        UserScope? r = await sut.GetUserByNameAsync("sam");

        Assert.Same(expected, r);
    }

    [Fact]
    public async Task GetUserByNameAsync_Should_RejectBlankName()
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByNameAsync("  "));
    }

    /// <summary>Positive id delegates to store.</summary>
    [Fact]
    public async Task GetUserByIdAsync_Should_ReturnScope_FromStore()
    {
        UserScope expected = new() { UserId = 3, Email = "e@e.e", Name = "E" };
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        UserService sut = CreateUserService(db.Object);
        UserScope? r = await sut.GetUserByIdAsync(3);

        Assert.Same(expected, r);
    }

    [Fact]
    public async Task GetUserByEmailAsync_Should_ReturnScope_FromStore_WhenNormalized()
    {
        UserScope expected = new() { UserId = 9, Email = "a@b.c", Name = "A" };
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserByEmailAsync("a@b.c", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        UserService sut = CreateUserService(db.Object);
        UserScope? r = await sut.GetUserByEmailAsync("a@b.c");

        Assert.Same(expected, r);
    }

    [Fact]
    public async Task SendVerificationMailAsync_Should_EnqueueJob_WhenUserExists()
    {
        Mock<IHermesDataStore> db = new();
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
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserService sut = CreateUserService(db.Object);

        await Assert.ThrowsAsync<UserNotFoundException>(() => sut.SendVerificationMailAsync("ghost@test.dev", CancellationToken.None));
    }

    [Fact]
    public async Task SendVerificationMailAsync_Should_RejectBlankEmail()
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SendVerificationMailAsync("  ", CancellationToken.None));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public async Task CheckVerificationCodeAsync_Should_RejectInvalidCode(int invalidCode)
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.CheckVerificationCodeAsync(1, invalidCode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task CheckVerificationCodeAsync_Should_RejectInvalidUserId(int invalidUserId)
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.CheckVerificationCodeAsync(invalidUserId, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowUserNotFound_WhenUserMissing()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserService sut = CreateUserService(db.Object);

        await Assert.ThrowsAsync<UserNotFoundException>(() => sut.CheckVerificationCodeAsync(5, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenNoChallengeStored()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, TwoFactorCode = null, TwoFactorExpiry = null });

        UserService sut = CreateUserService(db.Object);

        await Assert.ThrowsAsync<VerificationCodeMismatchException>(() => sut.CheckVerificationCodeAsync(1, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenExpired()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = 1,
                TwoFactorCode = "123456",
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(-5),
            });

        UserService sut = CreateUserService(db.Object);

        await Assert.ThrowsAsync<VerificationCodeMismatchException>(() => sut.CheckVerificationCodeAsync(1, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenCodeWrong()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = 1,
                TwoFactorCode = "999999",
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(10),
            });

        UserService sut = CreateUserService(db.Object);

        await Assert.ThrowsAsync<VerificationCodeMismatchException>(() => sut.CheckVerificationCodeAsync(1, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_CompleteVerification_WhenCodeAndExpiryValid()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByIdAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = 8,
                TwoFactorCode = " 123456 ",
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });
        db.Setup(dataStore => dataStore.CompleteUserEmailVerificationAsync(8, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);

        await sut.CheckVerificationCodeAsync(8, 123456);

        db.Verify(dataStore => dataStore.CompleteUserEmailVerificationAsync(8, It.IsAny<CancellationToken>()), Times.Once);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetUserByIdAsync_Should_RejectNonPositiveId(int invalidId)
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByIdAsync(invalidId));
    }

    /// <summary>
    /// Email lookup rejects blank/whitespace input.
    /// </summary>
    [Fact]
    public async Task GetUserByEmailAsync_Should_RejectBlankEmail()
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByEmailAsync("  "));
    }

    /// <summary>
    /// Delete delegates to store when scope is provided (authorization assumed upstream).
    /// </summary>
    [Fact]
    public async Task DeleteUserAsync_Should_DelegateToStore_WhenScopeValid()
    {
        Mock<IHermesDataStore> db = new();
        UserScope scope = new() { UserId = 1, Email = "a@b", Name = "A" };
        db.Setup(dataStore => dataStore.DeleteUserAsync(scope, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        await sut.DeleteUserAsync(scope);

        db.Verify(dataStore => dataStore.DeleteUserAsync(scope, It.IsAny<CancellationToken>()), Times.Once);
    }
}
