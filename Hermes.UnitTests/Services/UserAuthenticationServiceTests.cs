using FluentResults;
using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.User;
using Hermes.Application.Errors;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Users;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Contains unit tests for <see cref="UserAuthenticationService"/>,
/// testing user self-registration, BCrypt-verified logins, profile updates, and session token invalidation.
/// </summary>
public sealed class UserAuthenticationServiceTests
{
    private static UserAuthenticationService CreateService(
        IUserRepository db,
        IRefreshTokenRepository? refreshTokens = null,
        IPasswordHasher? passwordHasher = null) =>
        new(db, refreshTokens ?? Mock.Of<IRefreshTokenRepository>(), passwordHasher ?? new Hermes.Infrastructure.Adapters.Outbound.Security.BCryptPasswordHasher());

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.RegisterUserAsync"/> normalizes email addresses,
    /// hashes the plaintext password via BCrypt, and sets user ID from the database response.
    /// </summary>
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
        Result<UserScopeDto> result = await sut.RegisterUserAsync(user);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("hello@test.com", result.Value.Email);
        db.Verify(dataStore => dataStore.SetUserAsync(
            It.Is<User>(registeredUser => BCrypt.Net.BCrypt.Verify("plain-secret", registeredUser.PasswordHash)),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(100, result.Value.UserId);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.RegisterUserAsync"/> returns a validation error when the request DTO is null.
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_Should_Fail_WhenRequestIsNull()
    {
        // Arrange
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act
        Result<UserScopeDto> result = await sut.RegisterUserAsync(null!);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Request cannot be null", result.Errors[0].Message);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.RegisterUserAsync"/> rejects a whitespace-only display name.
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_Should_RejectWhitespaceOnlyDisplayName()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        UserAuthenticationService sut = CreateService(db.Object);
        RegisterUserRequestDto user = new() { Name = "   ", Email = "ok@test.dev", Password = "pw" };

        // Act
        Result<UserScopeDto> result = await sut.RegisterUserAsync(user);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("User name is required", result.Errors[0].Message);
        db.Verify(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.RegisterUserAsync"/> fails when email is missing or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterUserAsync_Should_Fail_WhenEmailIsNullOrWhitespace(string? email)
    {
        // Arrange
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());
        RegisterUserRequestDto user = new() { Name = "Max", Email = email!, Password = "pw" };

        // Act
        Result<UserScopeDto> result = await sut.RegisterUserAsync(user);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("email is required", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.RegisterUserAsync"/> returns a duplicate email error
    /// when the requested email already exists in the system.
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_Should_Fail_WhenEmailAlreadyExists()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(repo => repo.GetUserByEmailAsync("existing@hermes.de", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserScopeDto { UserId = 5, Email = "existing@hermes.de", Name = "Existing" });

        UserAuthenticationService sut = CreateService(db.Object);
        RegisterUserRequestDto user = new() { Name = "New", Email = "existing@hermes.de", Password = "pw" };

        // Act
        Result<UserScopeDto> result = await sut.RegisterUserAsync(user);

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<DuplicateEmailError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.RegisterUserAsync"/> fails when the persistence layer does not assign a positive user ID.
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_Should_Fail_WhenDatabaseLeavesIdAtZero()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.SetUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);
        RegisterUserRequestDto user = new() { Name = "A", Email = "a@b.c", Password = "x" };

        // Act
        Result<UserScopeDto> result = await sut.RegisterUserAsync(user);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Failed to create user", result.Errors[0].Message);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.LoginAsync"/> fails when the identifier argument is blank.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenIdentifierBlank()
    {
        // Arrange
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act
        Result<LoginResultDto> result = await sut.LoginAsync("   ", "pw");

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("required", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.LoginAsync"/> fails when the password argument is blank.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenPasswordBlank()
    {
        // Arrange
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act
        Result<LoginResultDto> result = await sut.LoginAsync("user", "");

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("required", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.LoginAsync"/> routes lookup to the email repository method
    /// when the identifier contains an '@' character.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_LookupByEmail_WhenIdentifierContainsAtSign()
    {
        // Arrange
        string hash = BCrypt.Net.BCrypt.HashPassword("good");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync("me@test.dev", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(3), Email = Email.Parse("me@test.dev"), PasswordHash = hash, Name = "Me" });

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result<LoginResultDto> result = await sut.LoginAsync(" me@test.dev ", "good");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        Assert.Equal(3, result.Value.UserId);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.LoginAsync"/> routes lookup to the username repository method
    /// when the identifier lacks an '@' character.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_LookupByName_WhenIdentifierHasNoAtSign()
    {
        // Arrange
        string hash = BCrypt.Net.BCrypt.HashPassword("pw");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("alice", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(2), Email = Email.Parse("a@b.c"), PasswordHash = hash, Name = "alice" });

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result<LoginResultDto> result = await sut.LoginAsync("alice", "pw");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        db.Verify(dataStore => dataStore.GetUserEntityForAuthenticationByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.LoginAsync"/> returns a generic invalid credentials error
    /// on invalid passwords to prevent timing/enumeration attacks.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_NotRevealWhetherAccountExists_OnFailure()
    {
        // Arrange
        string hash = BCrypt.Net.BCrypt.HashPassword("right");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), PasswordHash = hash, Name = "bob", Email = Email.Parse("b@c.d") });

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result<LoginResultDto> result = await sut.LoginAsync("bob", "wrong");

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<InvalidCredentialsError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.LoginAsync"/> fails when the user does not exist.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenUserNotFound()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result<LoginResultDto> result = await sut.LoginAsync("unknown", "pw");

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<InvalidCredentialsError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.LoginAsync"/> fails when the stored password hash is empty.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenStoredPasswordHashIsEmpty()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Name = "bob", Email = Email.Parse("b@c.d"), PasswordHash = "" });
        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result<LoginResultDto> result = await sut.LoginAsync("bob", "pw");

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<InvalidCredentialsError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.LoginAsync"/> catches format/parsing exceptions
    /// from corrupted BCrypt hashes and cleanly fails with <see cref="InvalidCredentialsError"/>.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Should_Fail_WhenBCryptThrowsExceptionForCorruptHash()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityForAuthenticationByNameAsync("bob", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Name = "bob", Email = Email.Parse("b@c.d"), PasswordHash = "invalid_hash_format" });
        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result<LoginResultDto> result = await sut.LoginAsync("bob", "pw");

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<InvalidCredentialsError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.UpdateUserAsync"/> hashes the new password
    /// when the current password has been verified.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_HashNewPassword_WhenCurrentPasswordVerified()
    {
        // Arrange
        string existingHash = BCrypt.Net.BCrypt.HashPassword("oldpw");
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(5), Email = Email.Parse("x@y.z"), Name = "X", PasswordHash = existingHash });
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.UpdateUserAsync(userId: 5, name: "X", email: "x@y.z", newPasswordPlain: "new-secret", currentPasswordPlain: "oldpw");

        // Assert
        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.UpdateUserAsync(It.Is<User>(u => BCrypt.Net.BCrypt.Verify("new-secret", u.PasswordHash)), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.UpdateUserAsync"/> fails when name is missing or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateUserAsync_Should_Fail_WhenNameIsNullOrWhitespace(string? name)
    {
        // Arrange
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act
        Result result = await sut.UpdateUserAsync(userId: 1, name: name!, email: "a@b.c", newPasswordPlain: null);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Name is required", result.Errors[0].Message);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.UpdateUserAsync"/> fails when email is missing or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateUserAsync_Should_Fail_WhenEmailIsNullOrWhitespace(string? email)
    {
        // Arrange
        UserAuthenticationService sut = CreateService(Mock.Of<IUserRepository>());

        // Act
        Result result = await sut.UpdateUserAsync(userId: 1, name: "Max", email: email!, newPasswordPlain: null);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Email is required", result.Errors[0].Message);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.UpdateUserAsync"/> requires the current password
    /// whenever setting a new password.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_RequireCurrentPassword_WhenChangingPassword()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = Email.Parse("a@b.c"), Name = "N", PasswordHash = "old" });
        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.UpdateUserAsync(userId: 1, name: "N", email: "a@b.c", newPasswordPlain: "new", currentPasswordPlain: null);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Current password is required", result.Errors[0].Message);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.UpdateUserAsync"/> rejects an incorrect current password.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_RejectWrongCurrentPassword()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(9), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(9), Email = Email.Parse("e@f.g"), Name = "E", PasswordHash = BCrypt.Net.BCrypt.HashPassword("real") });

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.UpdateUserAsync(userId: 9, name: "E", email: "e@f.g", newPasswordPlain: "hacker", currentPasswordPlain: "wrong-old");

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<InvalidCurrentPasswordError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.UpdateUserAsync"/> fails when the target user is not found.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_Fail_WhenChangingPassword_AndUserMissing()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(404), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.UpdateUserAsync(userId: 404, name: "N", email: "a@b.c", newPasswordPlain: "new-Valid_9!", currentPasswordPlain: "old");

        // Assert
        Assert.True(result.IsFailed);
        Assert.IsType<UserNotFoundError>(result.Errors.First());
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.UpdateUserAsync"/> fails when the existing account has no stored password hash.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_Fail_WhenStoredPasswordHashEmpty()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = Email.Parse("a@b.c"), Name = "N", PasswordHash = string.Empty });

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.UpdateUserAsync(userId: 1, name: "N", email: "a@b.c", newPasswordPlain: "new-Valid_9!", currentPasswordPlain: "anything");

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("no password is set", result.Errors[0].Message);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.UpdateUserAsync"/> updates name and email
    /// without touching password hash when new password is omitted.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_UpdateWithoutPassword_WhenNewPasswordOmitted()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(2), Email = Email.Parse("u@x.y"), Name = "OnlyName", PasswordHash = "hash" });

        UserAuthenticationService sut = CreateService(db.Object);

        // Act
        Result result = await sut.UpdateUserAsync(userId: 2, name: "OnlyName", email: "u@x.y", newPasswordPlain: null, currentPasswordPlain: null);

        // Assert
        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(dataStore => dataStore.UpdateUserAsync(It.Is<User>(u => u.PasswordHash == "hash"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.UpdateUserAsync"/> revokes all active refresh tokens for the user
    /// when the password has been successfully modified.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_RevokeAllRefreshTokens_WhenPasswordChanged()
    {
        // Arrange
        string currentHash = BCrypt.Net.BCrypt.HashPassword("current-secret-123");
        Mock<IUserRepository> db = new();
        Mock<IRefreshTokenRepository> refreshTokens = new();

        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(5), Email = Email.Parse("u@test.dev"), Name = "User5", PasswordHash = currentHash });
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        refreshTokens.Setup(repo => repo.RevokeAllRefreshTokensForUserAsync(new UserId(5), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object, refreshTokens.Object);

        // Act
        Result result = await sut.UpdateUserAsync(
            userId: 5,
            name: "User5",
            email: "u@test.dev",
            newPasswordPlain: "new-secret-456",
            currentPasswordPlain: "current-secret-123");

        // Assert
        Assert.True(result.IsSuccess);
        refreshTokens.Verify(repo => repo.RevokeAllRefreshTokensForUserAsync(new UserId(5), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="UserAuthenticationService.UpdateUserAsync"/> does not revoke refresh tokens
    /// when only name or email is modified and password remains unchanged.
    /// </summary>
    [Fact]
    public async Task UpdateUserAsync_Should_NotRevokeRefreshTokens_WhenPasswordNotChanged()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        Mock<IRefreshTokenRepository> refreshTokens = new();

        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(5), Email = Email.Parse("u@test.dev"), Name = "User5", PasswordHash = "hash" });
        db.Setup(dataStore => dataStore.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        UserAuthenticationService sut = CreateService(db.Object, refreshTokens.Object);

        // Act
        Result result = await sut.UpdateUserAsync(
            userId: 5,
            name: "NewName",
            email: "u@test.dev",
            newPasswordPlain: null,
            currentPasswordPlain: null);

        // Assert
        Assert.True(result.IsSuccess);
        refreshTokens.Verify(repo => repo.RevokeAllRefreshTokensForUserAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
