using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Users;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class UserAuthenticationServiceTests
{
    private static UserAuthenticationService CreateService(IUserRepository db) => new(db);

    // [R]IGHT: Normalizes email and stores BCrypt hash of plain text password
    [Fact]
    public async Task RegisterUserAsync_Should_NormalizeEmail_AndStoreOnlyBcryptHashOfPassword()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = new UserId(100))
            .Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);
        RegisterUserRequestDto user = new()
        {
            Name = "Tester",
            Email = "  Hello@Test.COM ",
            Password = "plain-secret",
        };

        // Act
        UserScopeDto scope = await sut.RegisterUserAsync(user);

        // Assert
        Assert.Equal("hello@test.com", scope.Email);
        db.Verify(dataStore => dataStore.SetUserAsync(
            It.Is<User>(registeredUser => BCrypt.Net.BCrypt.Verify("plain-secret", registeredUser.PasswordHash)),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(100, scope.UserId);
    }

    // [B]OUNDARY: Rejects whitespace-only display name input
    [Fact]
    public async Task RegisterUserAsync_Should_RejectWhitespaceOnlyDisplayName()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = new UserId(5))
            .Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);
        RegisterUserRequestDto user = new() { Name = "   ", Email = "ok@test.dev", Password = "pw" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.RegisterUserAsync(user));
        db.Verify(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // [E]RROR: Throws exception when repository fails to return a positive user ID
    [Fact]
    public async Task RegisterUserAsync_Should_Fail_WhenDatabaseLeavesIdAtZero()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);
        RegisterUserRequestDto user = new() { Name = "A", Email = "a@b.c", Password = "x" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.RegisterUserAsync(user));
    }

    // [B]OUNDARY: Fails authentication when username/email identifier is blank
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenIdentifierBlank()
    {
        // Arrange
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act
        LoginResultDto loginResult = await sut.LoginAsync("   ", "pw");

        // Assert
        Assert.False(loginResult.Success);
        Assert.Contains("required", loginResult.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    // [B]OUNDARY: Fails authentication when password is blank
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenPasswordBlank()
    {
        // Arrange
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act
        LoginResultDto loginResult = await sut.LoginAsync("user", "");

        // Assert
        Assert.False(loginResult.Success);
        Assert.False(string.IsNullOrEmpty(loginResult.ErrorMessage));
    }

    // [R]IGHT: Looks up account by email address when identifier contains '@'
    [Fact]
    public async Task LoginAsync_Should_LookupByEmail_WhenIdentifierContainsAtSign()
    {
        // Arrange
        string hash = BCrypt.Net.BCrypt.HashPassword("good");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync("me@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(3), Email = "me@test.dev", PasswordHash = hash, Name = "Me" });

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        LoginResultDto loginResult = await sut.LoginAsync(" me@test.dev ", "good");

        // Assert
        Assert.True(loginResult.Success);
        Assert.Equal(3, loginResult.UserId);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // [R]IGHT: Looks up account by display name when identifier does not contain '@'
    [Fact]
    public async Task LoginAsync_Should_LookupByName_WhenIdentifierHasNoAtSign()
    {
        // Arrange
        string hash = BCrypt.Net.BCrypt.HashPassword("pw");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(2), Email = "a@b.c", PasswordHash = hash, Name = "alice" });

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        LoginResultDto loginResult = await sut.LoginAsync("alice", "pw");

        // Assert
        Assert.True(loginResult.Success);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // [E]RROR: Returns generic failure error message when authentication fails to prevent account enumeration
    [Fact]
    public async Task LoginAsync_Should_NotRevealWhetherAccountExists_OnFailure()
    {
        // Arrange
        string hash = BCrypt.Net.BCrypt.HashPassword("right");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), PasswordHash = hash, Name = "bob", Email = "b@c.d" });

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        LoginResultDto loginResult = await sut.LoginAsync("bob", "wrong");

        // Assert
        Assert.False(loginResult.Success);
        Assert.Equal("Invalid login or password.", loginResult.ErrorMessage);
    }

    // [B]OUNDARY: Fails authentication when user account is not found in database
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenUserNotFound()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        LoginResultDto loginResult = await sut.LoginAsync("unknown", "pw");

        // Assert
        Assert.False(loginResult.Success);
        Assert.Equal("Invalid login or password.", loginResult.ErrorMessage);
    }

    // [B]OUNDARY: Fails authentication when stored password hash is empty
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenStoredPasswordHashIsEmpty()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Name = "bob", Email = "b@c.d", PasswordHash = "" });
        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        LoginResultDto loginResult = await sut.LoginAsync("bob", "pw");

        // Assert
        Assert.False(loginResult.Success);
        Assert.Equal("Invalid login or password.", loginResult.ErrorMessage);
    }

    // [E]RROR: Safely returns authentication failure when BCrypt throws exception on corrupted hash string
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenBCryptThrowsExceptionForCorruptHash()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Name = "bob", Email = "b@c.d", PasswordHash = "invalid_hash_format" });
        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        LoginResultDto loginResult = await sut.LoginAsync("bob", "pw");

        // Assert
        Assert.False(loginResult.Success);
        Assert.Equal("Invalid login or password.", loginResult.ErrorMessage);
    }

    // [R]IGHT: Hashes new password with BCrypt after verifying current password
    [Fact]
    public async Task UpdateUserAsync_Should_HashNewPassword_WhenCurrentPasswordVerified()
    {
        // Arrange
        string existingHash = BCrypt.Net.BCrypt.HashPassword("oldpw");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(5), Email = "x@y.z", Name = "X", PasswordHash = existingHash });
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);
        User patch = new() { Id = new UserId(5), Email = "x@y.z", Name = "X", PasswordHash = "new-secret" };

        // Act
        await sut.UpdateUserAsync(patch, currentPasswordPlain: "oldpw");

        // Assert
        db.Verify(dataStore => dataStore.UpdateUserAsync(It.Is<User>(u => BCrypt.Net.BCrypt.Verify("new-secret", u.PasswordHash)), It.IsAny<CancellationToken>()), Times.Once);
    }

    // [E]RROR: Throws exception when current password is omitted during password change
    [Fact]
    public async Task UpdateUserAsync_Should_RequireCurrentPassword_WhenChangingPassword()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = "a@b.c", Name = "N", PasswordHash = "old" });
        UserAuthenticationService sut = CreateService(db.Object);
        User patch = new() { Id = new UserId(1), Email = "a@b.c", Name = "N", PasswordHash = "new" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: null));
    }

    // [E]RROR: Throws exception when provided current password does not match stored hash
    [Fact]
    public async Task UpdateUserAsync_Should_RejectWrongCurrentPassword()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(9), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(9), Email = "e@f.g", Name = "E", PasswordHash = BCrypt.Net.BCrypt.HashPassword("real") });

        UserAuthenticationService sut = CreateService(db.Object);
        User patch = new() { Id = new UserId(9), Email = "e@f.g", Name = "E", PasswordHash = "hacker" };

        // Act & Assert
        await Assert.ThrowsAsync<WrongCurrentPasswordException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "wrong-old"));
    }

    // [E]RROR: Throws UserNotFoundException when updating missing user account
    [Fact]
    public async Task UpdateUserAsync_Should_ThrowUserNotFound_WhenChangingPassword_AndUserMissing()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(404), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserAuthenticationService sut = CreateService(db.Object);
        User patch = new() { Id = new UserId(404), Email = "a@b.c", Name = "N", PasswordHash = "new-Valid_9!" };

        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "old"));
    }

    // [E]RROR: Throws InvalidOperationException when user account has no password set
    [Fact]
    public async Task UpdateUserAsync_Should_ThrowInvalidOperation_WhenStoredPasswordHashEmpty()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = "a@b.c", Name = "N", PasswordHash = null });

        UserAuthenticationService sut = CreateService(db.Object);
        User patch = new() { Id = new UserId(1), Email = "a@b.c", Name = "N", PasswordHash = "new-Valid_9!" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "anything"));
    }

    // [R]IGHT: Updates profile fields without touching password when no new password is provided
    [Fact]
    public async Task UpdateUserAsync_Should_UpdateWithoutPassword_WhenNewPasswordOmitted()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(2), Email = "u@x.y", Name = "OnlyName", PasswordHash = "hash" });

        UserAuthenticationService sut = CreateService(db.Object);
        User patch = new() { Id = new UserId(2), Email = "u@x.y", Name = "OnlyName", PasswordHash = null };

        // Act
        await sut.UpdateUserAsync(patch, currentPasswordPlain: null);

        // Assert
        db.Verify(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(dataStore => dataStore.UpdateUserAsync(It.Is<User>(u => u.PasswordHash == "hash"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
