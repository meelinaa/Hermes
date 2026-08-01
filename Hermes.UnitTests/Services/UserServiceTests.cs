using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class UserServiceTests
{
    private static UserService CreateUserService(IUserRepository db) => new(db);

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
