using FluentResults;
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

    [Fact]
    public async Task RegisterUserAsync_Should_NormalizeEmail_AndStoreOnlyBcryptHashOfPassword()
    {
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

        Result<UserScopeDto> result = await sut.RegisterUserAsync(user);

        Assert.True(result.IsSuccess);
        Assert.Equal("hello@test.com", result.Value.Email);
        db.Verify(dataStore => dataStore.SetUserAsync(
            It.Is<User>(registeredUser => BCrypt.Net.BCrypt.Verify("plain-secret", registeredUser.PasswordHash)),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(100, result.Value.UserId);
    }

    [Fact]
    public async Task RegisterUserAsync_Should_RejectWhitespaceOnlyDisplayName()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => u.Id = new UserId(5))
            .Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);
        RegisterUserRequestDto user = new() { Name = "   ", Email = "ok@test.dev", Password = "pw" };

        Result<UserScopeDto> result = await sut.RegisterUserAsync(user);

        Assert.True(result.IsFailed);
        db.Verify(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterUserAsync_Should_Fail_WhenDatabaseLeavesIdAtZero()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);
        RegisterUserRequestDto user = new() { Name = "A", Email = "a@b.c", Password = "x" };

        Result<UserScopeDto> result = await sut.RegisterUserAsync(user);

        Assert.True(result.IsFailed);
        Assert.Contains("Failed", result.Errors[0].Message);
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenIdentifierBlank()
    {
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

        Result<LoginResultDto> result = await sut.LoginAsync("   ", "pw");

        Assert.True(result.IsFailed);
        Assert.Contains("required", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenPasswordBlank()
    {
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

        Result<LoginResultDto> result = await sut.LoginAsync("user", "");

        Assert.True(result.IsFailed);
        Assert.Contains("required", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_Should_LookupByEmail_WhenIdentifierContainsAtSign()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("good");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync("me@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(3), Email = Email.Parse("me@test.dev"), PasswordHash = hash, Name = "Me" });

        UserAuthenticationService sut = CreateService(db.Object);

        Result<LoginResultDto> result = await sut.LoginAsync(" me@test.dev ", "good");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        Assert.Equal(3, result.Value.UserId);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_Should_LookupByName_WhenIdentifierHasNoAtSign()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("pw");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(2), Email = Email.Parse("a@b.c"), PasswordHash = hash, Name = "alice" });

        UserAuthenticationService sut = CreateService(db.Object);

        Result<LoginResultDto> result = await sut.LoginAsync("alice", "pw");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_Should_NotRevealWhetherAccountExists_OnFailure()
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("right");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), PasswordHash = hash, Name = "bob", Email = Email.Parse("b@c.d") });

        UserAuthenticationService sut = CreateService(db.Object);

        Result<LoginResultDto> result = await sut.LoginAsync("bob", "wrong");

        Assert.True(result.IsFailed);
        Assert.Equal("Invalid login or password.", result.Errors[0].Message);
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenUserNotFound()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        UserAuthenticationService sut = CreateService(db.Object);

        Result<LoginResultDto> result = await sut.LoginAsync("unknown", "pw");

        Assert.True(result.IsFailed);
        Assert.Equal("Invalid login or password.", result.Errors[0].Message);
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenStoredPasswordHashIsEmpty()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Name = "bob", Email = Email.Parse("b@c.d"), PasswordHash = "" });
        UserAuthenticationService sut = CreateService(db.Object);

        Result<LoginResultDto> result = await sut.LoginAsync("bob", "pw");

        Assert.True(result.IsFailed);
        Assert.Equal("Invalid login or password.", result.Errors[0].Message);
    }

    [Fact]
    public async Task LoginAsync_Should_Fail_WhenBCryptThrowsExceptionForCorruptHash()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Name = "bob", Email = Email.Parse("b@c.d"), PasswordHash = "invalid_hash_format" });
        UserAuthenticationService sut = CreateService(db.Object);

        Result<LoginResultDto> result = await sut.LoginAsync("bob", "pw");

        Assert.True(result.IsFailed);
        Assert.Equal("Invalid login or password.", result.Errors[0].Message);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_HashNewPassword_WhenCurrentPasswordVerified()
    {
        string existingHash = BCrypt.Net.BCrypt.HashPassword("oldpw");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(5), Email = Email.Parse("x@y.z"), Name = "X", PasswordHash = existingHash });
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);

        Result result = await sut.UpdateUserAsync(userId: 5, name: "X", email: "x@y.z", newPasswordPlain: "new-secret", currentPasswordPlain: "oldpw");

        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.UpdateUserAsync(It.Is<User>(u => BCrypt.Net.BCrypt.Verify("new-secret", u.PasswordHash)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_RequireCurrentPassword_WhenChangingPassword()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = Email.Parse("a@b.c"), Name = "N", PasswordHash = "old" });
        UserAuthenticationService sut = CreateService(db.Object);

        Result result = await sut.UpdateUserAsync(userId: 1, name: "N", email: "a@b.c", newPasswordPlain: "new", currentPasswordPlain: null);
        
        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_RejectWrongCurrentPassword()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(9), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(9), Email = Email.Parse("e@f.g"), Name = "E", PasswordHash = BCrypt.Net.BCrypt.HashPassword("real") });

        UserAuthenticationService sut = CreateService(db.Object);

        Result result = await sut.UpdateUserAsync(userId: 9, name: "E", email: "e@f.g", newPasswordPlain: "hacker", currentPasswordPlain: "wrong-old");
        
        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_Fail_WhenChangingPassword_AndUserMissing()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(404), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserAuthenticationService sut = CreateService(db.Object);

        Result result = await sut.UpdateUserAsync(userId: 404, name: "N", email: "a@b.c", newPasswordPlain: "new-Valid_9!", currentPasswordPlain: "old");
        
        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_Fail_WhenStoredPasswordHashEmpty()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = Email.Parse("a@b.c"), Name = "N", PasswordHash = null });

        UserAuthenticationService sut = CreateService(db.Object);

        Result result = await sut.UpdateUserAsync(userId: 1, name: "N", email: "a@b.c", newPasswordPlain: "new-Valid_9!", currentPasswordPlain: "anything");
        
        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task UpdateUserAsync_Should_UpdateWithoutPassword_WhenNewPasswordOmitted()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(2), Email = Email.Parse("u@x.y"), Name = "OnlyName", PasswordHash = "hash" });

        UserAuthenticationService sut = CreateService(db.Object);

        Result result = await sut.UpdateUserAsync(userId: 2, name: "OnlyName", email: "u@x.y", newPasswordPlain: null, currentPasswordPlain: null);

        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(dataStore => dataStore.UpdateUserAsync(It.Is<User>(u => u.PasswordHash == "hash"), It.IsAny<CancellationToken>()), Times.Once);
    }
}

