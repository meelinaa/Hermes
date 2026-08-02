using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class UserServiceTests
{
    private static UserService CreateUserService(IUserRepository db) => new(db);

    // [R]IGHT: Retrieves matching user scope by display name
    [Fact]
    public async Task GetUserByNameAsync_Should_ReturnScope_FromStore()
    {
        // Arrange
        UserScopeDto expected = new() { UserId = 7, Name = "Sam", Email = "sam@test.dev" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserByNameAsync("sam", It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        UserService sut = CreateUserService(db.Object);

        // Act
        UserScopeDto? r = await sut.GetUserByNameAsync("sam");

        // Assert
        Assert.Same(expected, r);
    }

    // [B]OUNDARY: Rejects empty or whitespace-only display name input
    [Fact]
    public async Task GetUserByNameAsync_Should_RejectBlankName()
    {
        // Arrange
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByNameAsync("  "));
    }

    // [R]IGHT: Retrieves matching user scope by unique identifier
    [Fact]
    public async Task GetUserByIdAsync_Should_ReturnScope_FromStore()
    {
        // Arrange
        UserScopeDto expected = new() { UserId = 3, Email = "e@e.e", Name = "E" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        UserService sut = CreateUserService(db.Object);

        // Act
        UserScopeDto? r = await sut.GetUserByIdAsync(3);

        // Assert
        Assert.Same(expected, r);
    }

    // [R]IGHT: Retrieves matching user scope by email address
    [Fact]
    public async Task GetUserByEmailAsync_Should_ReturnScope_FromStore_WhenNormalized()
    {
        // Arrange
        UserScopeDto expected = new() { UserId = 9, Email = "a@b.c", Name = "A" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserByEmailAsync("a@b.c", It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        UserService sut = CreateUserService(db.Object);

        // Act
        UserScopeDto? r = await sut.GetUserByEmailAsync("a@b.c");

        // Assert
        Assert.Same(expected, r);
    }

    // [B]OUNDARY: Rejects non-positive user ID inputs
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetUserByIdAsync_Should_RejectNonPositiveId(int invalidId)
    {
        // Arrange
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByIdAsync(invalidId));
    }

    // [B]OUNDARY: Rejects empty or whitespace-only email input
    [Fact]
    public async Task GetUserByEmailAsync_Should_RejectBlankEmail()
    {
        // Arrange
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetUserByEmailAsync("  "));
    }

    // [R]IGHT: Delegates user deletion operation to underlying repository store
    [Fact]
    public async Task DeleteUserAsync_Should_DelegateToStore_WhenScopeValid()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        UserScopeDto scope = new() { UserId = 1, Email = "a@b", Name = "A" };
        db.Setup(dataStore => dataStore.DeleteUserAsync(scope, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UserService sut = CreateUserService(db.Object);

        // Act
        await sut.DeleteUserAsync(scope);

        // Assert
        db.Verify(dataStore => dataStore.DeleteUserAsync(scope, It.IsAny<CancellationToken>()), Times.Once);
    }
}
