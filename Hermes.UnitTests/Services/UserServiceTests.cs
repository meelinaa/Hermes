using FluentResults;
using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Users;
using Hermes.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class UserServiceTests
{
    private static UserService CreateUserService(IUserRepository db) => new(db);

    [Fact]
    public async Task GetUserByNameAsync_Should_ReturnScope_FromStore()
    {
        UserScopeDto expected = new() { UserId = 7, Name = "Sam", Email = Email.Parse("sam@test.dev") };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserByNameAsync("sam", It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        UserService sut = CreateUserService(db.Object);

        Result<UserScopeDto> r = await sut.GetUserByNameAsync("sam");

        Assert.True(r.IsSuccess);
        Assert.Same(expected, r.Value);
    }

    [Fact]
    public async Task GetUserByNameAsync_Should_RejectBlankName()
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());

        Result<UserScopeDto> r = await sut.GetUserByNameAsync("  ");
        
        Assert.True(r.IsFailed);
    }
    
    [Fact]
    public async Task GetUserByNameAsync_Should_ReturnFailed_WhenNotFound()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserByNameAsync("sam", It.IsAny<CancellationToken>())).ReturnsAsync((UserScopeDto?)null);
        UserService sut = CreateUserService(db.Object);

        Result<UserScopeDto> r = await sut.GetUserByNameAsync("sam");

        Assert.True(r.IsFailed);
    }

    [Fact]
    public async Task GetUserByIdAsync_Should_ReturnScope_FromStore()
    {
        UserScopeDto expected = new() { UserId = 3, Email = Email.Parse("e@e.e"), Name = "E" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserByIdAsync(new UserId(3), It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        UserService sut = CreateUserService(db.Object);

        Result<UserScopeDto> r = await sut.GetUserByIdAsync(new UserId(3));

        Assert.True(r.IsSuccess);
        Assert.Same(expected, r.Value);
    }

    [Fact]
    public async Task GetUserByEmailAsync_Should_ReturnScope_FromStore_WhenNormalized()
    {
        UserScopeDto expected = new() { UserId = 9, Email = Email.Parse("a@b.c"), Name = "A" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserByEmailAsync("a@b.c", It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        UserService sut = CreateUserService(db.Object);

        Result<UserScopeDto> r = await sut.GetUserByEmailAsync("a@b.c");

        Assert.True(r.IsSuccess);
        Assert.Same(expected, r.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetUserByIdAsync_Should_RejectNonPositiveId(int invalidId)
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());

        Result<UserScopeDto> r = await sut.GetUserByIdAsync(new UserId(invalidId));
        
        Assert.True(r.IsFailed);
    }

    [Fact]
    public async Task GetUserByEmailAsync_Should_RejectBlankEmail()
    {
        UserService sut = CreateUserService(Mock.Of<IUserRepository>());

        Result<UserScopeDto> r = await sut.GetUserByEmailAsync("  ");
        
        Assert.True(r.IsFailed);
    }

    [Fact]
    public async Task DeleteUserAsync_Should_DelegateToStore_WhenScopeValid()
    {
        Mock<IUserRepository> db = new();
        UserScopeDto scope = new() { UserId = 1, Email = Email.Parse("a@b"), Name = "A" };
        db.Setup(dataStore => dataStore.DeleteUserAsync(scope, It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        UserService sut = CreateUserService(db.Object);

        Result r = await sut.DeleteUserAsync(scope);

        Assert.True(r.IsSuccess);
        db.Verify(dataStore => dataStore.DeleteUserAsync(scope, It.IsAny<CancellationToken>()), Times.Once);
    }
}
