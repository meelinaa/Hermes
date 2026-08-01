using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class UserAuthenticationServiceTests
{
    private static UserAuthenticationService CreateService(IUserRepository db) => new(db);

    [Fact]
    public async Task RegisterUserAsync_Should_NormalizeEmail_AndStoreOnlyBcryptHashOfPassword()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = 100)
            .Returns(Task.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);
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

        UserAuthenticationService sut = CreateService(db.Object);
        RegisterUserRequestDto user = new() { Name = "   ", Email = "ok@test.dev", Password = "pw" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RegisterUserAsync(user));
        db.Verify(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterUserAsync_Should_Fail_WhenDatabaseLeavesIdAtZero()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);
        RegisterUserRequestDto user = new() { Name = "A", Email = "a@b.c", Password = "x" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RegisterUserAsync(user));
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenIdentifierBlank()
    {
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

        LoginResultDto loginResult = await sut.LoginAsync("   ", "pw");

        Assert.False(loginResult.Success);
        Assert.Contains("required", loginResult.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenPasswordBlank()
    {
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

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

        UserAuthenticationService sut = CreateService(db.Object);

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

        UserAuthenticationService sut = CreateService(db.Object);

        LoginResultDto loginResult = await sut.LoginAsync("alice", "pw");

        Assert.True(loginResult.Success);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_Should_NotRevealWhetherAccountExists_OnFailure()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("right");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, PasswordHash = hash, Name = "bob", Email = "b@c.d" });

        UserAuthenticationService sut = CreateService(db.Object);

        LoginResultDto loginResult = await sut.LoginAsync("bob", "wrong");

        Assert.False(loginResult.Success);
        Assert.Equal("Invalid login or password.", loginResult.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenUserNotFound()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        UserAuthenticationService sut = CreateService(db.Object);

        LoginResultDto loginResult = await sut.LoginAsync("unknown", "pw");

        Assert.False(loginResult.Success);
        Assert.Equal("Invalid login or password.", loginResult.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenStoredPasswordHashIsEmpty()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Name = "bob", Email = "b@c.d", PasswordHash = "" });
        UserAuthenticationService sut = CreateService(db.Object);

        LoginResultDto loginResult = await sut.LoginAsync("bob", "pw");

        Assert.False(loginResult.Success);
        Assert.Equal("Invalid login or password.", loginResult.ErrorMessage);
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenBCryptThrowsExceptionForCorruptHash()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Name = "bob", Email = "b@c.d", PasswordHash = "invalid_hash_format" });
        UserAuthenticationService sut = CreateService(db.Object);

        LoginResultDto loginResult = await sut.LoginAsync("bob", "pw");

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

        UserAuthenticationService sut = CreateService(db.Object);
        User patch = new() { Id = 5, Email = "x@y.z", Name = "X", PasswordHash = "new-secret" };

        await sut.UpdateUserAsync(patch, currentPasswordPlain: "oldpw");

        Assert.True(BCrypt.Net.BCrypt.Verify("new-secret", patch.PasswordHash));
        db.Verify(dataStore => dataStore.UpdateUserAsync(patch, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_RequireCurrentPassword_WhenChangingPassword()
    {
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());
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

        UserAuthenticationService sut = CreateService(db.Object);
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

        UserAuthenticationService sut = CreateService(db.Object);
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

        UserAuthenticationService sut = CreateService(db.Object);
        User patch = new() { Id = 1, Email = "a@b.c", Name = "N", PasswordHash = "new-Valid_9!" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.UpdateUserAsync(patch, currentPasswordPlain: "anything"));
    }

    [Fact]
    public async Task UpdateUserAsync_Should_UpdateWithoutPassword_WhenNewPasswordOmitted()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);
        User patch = new() { Id = 2, Email = "u@x.y", Name = "OnlyName", PasswordHash = null };

        await sut.UpdateUserAsync(patch, currentPasswordPlain: null);

        Assert.Null(patch.PasswordHash);
        db.Verify(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        db.Verify(dataStore => dataStore.UpdateUserAsync(patch, It.IsAny<CancellationToken>()), Times.Once);
    }
}
