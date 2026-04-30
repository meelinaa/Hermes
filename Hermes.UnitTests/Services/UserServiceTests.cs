using Hermes.Application.Models;
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
        db.Setup(x => x.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = 100)
            .Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        User user = new()
        {
            Name = "Tester",
            Email = "  Hello@Test.COM ",
            PasswordHash = "plain-secret",
        };

        // Act
        UserScope scope = await sut.RegisterUserAsync(user);

        // Assert
        Assert.Equal("hello@test.com", scope.Email);
        Assert.True(BCrypt.Net.BCrypt.Verify("plain-secret", user.PasswordHash));
        Assert.Equal(100, scope.UserId);
    }

    /// <summary>
    /// Display name cannot be whitespace-only; store must never be called (validation before persistence).
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_Should_RejectWhitespaceOnlyDisplayName()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = 5)
            .Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        User user = new() { Name = "   ", Email = "ok@test.dev", PasswordHash = "pw", Id = 0 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RegisterUserAsync(user));
        db.Verify(x => x.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// If SetUserAsync does not assign a positive id, registration fails (contract with persistence layer).
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_Should_Fail_WhenDatabaseLeavesIdAtZero()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        User user = new() { Name = "A", Email = "a@b.c", PasswordHash = "x", Id = 0 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RegisterUserAsync(user));
    }

    /// <summary>
    /// Blank identifier fails fast without querying the database.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenIdentifierBlank()
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());

        LoginResult r = await sut.LoginAsync("   ", "pw");

        Assert.False(r.Success);
        Assert.Contains("required", r.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Blank password fails without revealing whether the account exists.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenPasswordBlank()
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());

        LoginResult r = await sut.LoginAsync("user", "");

        Assert.False(r.Success);
        Assert.False(string.IsNullOrEmpty(r.ErrorMessage));
    }

    /// <summary>
    /// Identifier containing '@' is treated as email lookup (normalized trim).
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_LookupByEmail_WhenIdentifierContainsAt()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("good");
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityForAuthenticationByEmailAsync("me@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 3, Email = "me@test.dev", PasswordHash = hash, Name = "Me" });

        UserService sut = CreateUserService(db.Object);

        LoginResult r = await sut.LoginAsync(" me@test.dev ", "good");

        Assert.True(r.Success);
        Assert.Equal(3, r.UserId);
        db.Verify(x => x.GetUserEntityForAuthenticationByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Identifier without '@' uses display-name lookup path.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_LookupByName_WhenIdentifierHasNoAtSign()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("pw");
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityForAuthenticationByNameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 2, Email = "a@b.c", PasswordHash = hash, Name = "alice" });

        UserService sut = CreateUserService(db.Object);

        LoginResult r = await sut.LoginAsync("alice", "pw");

        Assert.True(r.Success);
        db.Verify(x => x.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Wrong password yields generic error message (no distinction from unknown user).
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_NotRevealWhetherAccountExists_OnFailure()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("right");
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, PasswordHash = hash, Name = "bob", Email = "b@c.d" });

        UserService sut = CreateUserService(db.Object);

        LoginResult r = await sut.LoginAsync("bob", "wrong");

        Assert.False(r.Success);
        Assert.Equal("Invalid login or password.", r.ErrorMessage);
    }

    /// <summary>
    /// Password change hashes new secret after verifying current password against BCrypt hash.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_HashNewPassword_WhenCurrentPasswordVerified()
    {
        string existingHash = BCrypt.Net.BCrypt.HashPassword("oldpw");
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, Email = "x@y.z", Name = "X", PasswordHash = existingHash });
        db.Setup(x => x.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 5, Email = "x@y.z", Name = "X", PasswordHash = "new-secret" };

        await sut.UpdateUserAsync(patch, currentPasswordPlain: "oldpw");

        Assert.True(BCrypt.Net.BCrypt.Verify("new-secret", patch.PasswordHash));
        db.Verify(x => x.UpdateUserAsync(patch, It.IsAny<CancellationToken>()), Times.Once);
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
        db.Setup(x => x.GetUserEntityByIdAsync(9, It.IsAny<CancellationToken>()))
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
        db.Setup(x => x.GetUserEntityByIdAsync(404, It.IsAny<CancellationToken>())) // User does not exist.
            .ReturnsAsync((User?)null);

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 404, Email = "a@b.c", Name = "N", PasswordHash = "new-Valid_9!" }; // New password is not relevant for this test.

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "old")); // Current password is not relevant for this test.
    }

    /// <summary>Cannot set new password if stored hash is missing (account without password).</summary>
    [Fact]
    public async Task UpdateUserAsync_Should_ThrowInvalidOperation_WhenStoredPasswordHashEmpty()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "a@b.c", Name = "N", PasswordHash = null }); // Stored password hash is empty.

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
        db.Setup(x => x.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask); // Update user does not require password verification.

        UserService sut = CreateUserService(db.Object);
        User patch = new() { Id = 2, Email = "u@x.y", Name = "OnlyName", PasswordHash = null };

        await sut.UpdateUserAsync(patch, currentPasswordPlain: null);

        Assert.Null(patch.PasswordHash);
        db.Verify(x => x.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(x => x.UpdateUserAsync(patch, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Name lookup delegates to store after trimming validation.</summary>
    [Fact]
    public async Task GetUserByNameAsync_Should_ReturnScope_FromStore()
    {
        UserScope expected = new() { UserId = 7, Name = "Sam", Email = "sam@test.dev" }; // User exists.
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserByNameAsync("sam", It.IsAny<CancellationToken>())).ReturnsAsync(expected); 

        UserService sut = CreateUserService(db.Object);

        UserScope? r = await sut.GetUserByNameAsync("sam"); // User exists.

        Assert.Same(expected, r);
    }

    [Fact]
    public async Task GetUserByNameAsync_Should_RejectBlankName()
    {
        UserService sut = CreateUserService(Mock.Of<IHermesDataStore>());
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByNameAsync("  ")); // Blank name. This should be rejected before querying the database.
    }

    /// <summary>Positive id delegates to store.</summary>
    [Fact]
    public async Task GetUserByIdAsync_Should_ReturnScope_FromStore()
    {
        UserScope expected = new() { UserId = 3, Email = "e@e.e", Name = "E" };
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        UserService sut = CreateUserService(db.Object);
        UserScope? r = await sut.GetUserByIdAsync(3); // User exists.

        Assert.Same(expected, r);
    }

    [Fact]
    public async Task GetUserByEmailAsync_Should_ReturnScope_FromStore_WhenNormalized()
    {
        UserScope expected = new() { UserId = 9, Email = "a@b.c", Name = "A" };
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserByEmailAsync("a@b.c", It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        UserService sut = CreateUserService(db.Object);
        UserScope? r = await sut.GetUserByEmailAsync("a@b.c"); // User exists.

        Assert.Same(expected, r);
    }

    [Fact]
    public async Task SendVerificationMailAsync_Should_EnqueueJob_WhenUserExists()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityForAuthenticationByEmailAsync("u@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 42, Email = "u@test.dev" });

        Mock<IVerificationMailJobTrigger> trigger = new();
        trigger.Setup(t => t.EnqueueSendVerificationMail(42)).Returns("job-1");

        UserService sut = CreateUserService(db.Object, trigger.Object);

        await sut.SendVerificationMailAsync("  U@Test.dev ", CancellationToken.None);

        trigger.Verify(t => t.EnqueueSendVerificationMail(42), Times.Once); // Verification mail job should be enqueued.
    }

    [Fact]
    public async Task SendVerificationMailAsync_Should_ThrowUserNotFound_WhenEmailUnknown()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserService sut = CreateUserService(db.Object);

        await Assert.ThrowsAsync<UserNotFoundException>(() => sut.SendVerificationMailAsync("ghost@test.dev", CancellationToken.None)); // User does not exist. 
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
        db.Setup(x => x.GetUserEntityForAuthenticationByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserService sut = CreateUserService(db.Object);

        await Assert.ThrowsAsync<UserNotFoundException>(() => sut.CheckVerificationCodeAsync(5, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenNoChallengeStored()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityForAuthenticationByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, TwoFactorCode = null, TwoFactorExpiry = null });

        UserService sut = CreateUserService(db.Object);

        await Assert.ThrowsAsync<VerificationCodeMismatchException>(() => sut.CheckVerificationCodeAsync(1, 123456));
    }

    [Fact]
    public async Task CheckVerificationCodeAsync_Should_ThrowVerificationCodeMismatch_WhenExpired()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityForAuthenticationByIdAsync(1, It.IsAny<CancellationToken>()))
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
        db.Setup(x => x.GetUserEntityForAuthenticationByIdAsync(1, It.IsAny<CancellationToken>()))
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
        db.Setup(x => x.GetUserEntityForAuthenticationByIdAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Id = 8,
                TwoFactorCode = " 123456 ",
                TwoFactorExpiry = DateTime.UtcNow.AddMinutes(5),
            });
        db.Setup(x => x.CompleteUserEmailVerificationAsync(8, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);

        await sut.CheckVerificationCodeAsync(8, 123456);

        db.Verify(x => x.CompleteUserEmailVerificationAsync(8, It.IsAny<CancellationToken>()), Times.Once);
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
        db.Setup(x => x.DeleteUserAsync(scope, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserService sut = CreateUserService(db.Object);
        await sut.DeleteUserAsync(scope);

        db.Verify(x => x.DeleteUserAsync(scope, It.IsAny<CancellationToken>()), Times.Once);
    }
}
