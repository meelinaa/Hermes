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
    private static UserService CreateUserService(IHermesDataStore db) =>
        new(db, Mock.Of<IVerificationMailJobTrigger>());

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

    /// <summary>
    /// Get by id rejects non-positive ids at the service boundary.
    /// </summary>
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
