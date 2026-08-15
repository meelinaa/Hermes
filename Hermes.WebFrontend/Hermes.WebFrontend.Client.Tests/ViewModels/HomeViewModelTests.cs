using Blazored.LocalStorage;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.User;
using Hermes.WebFrontend.Client.ViewModels;
using Moq;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.ViewModels;

/// <summary>
/// Unit tests verifying personalized greeting composition and profile refresh subscriptions in <see cref="HomeViewModel"/>.
/// </summary>
public sealed class HomeViewModelTests
{
    private const string ValidJwtWithName =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
        "eyJzdWIiOiI0MiIsIm5hbWUiOiJzb21ldXNlciJ9." +
        "dummySignature";

    private readonly Mock<IUserApiClient> _userApiMock = new();
    private readonly Mock<ILocalStorageService> _localStorageMock = new();
    private readonly UserProfileRefreshStore _profileRefresh = new();

    private HomeViewModel CreateSut(string? token = null)
    {
        _localStorageMock.Setup(s => s.GetItemAsync<string>("hermes.auth.accessToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        AuthTokenStore tokenStore = new(_localStorageMock.Object);
        return new HomeViewModel(_userApiMock.Object, tokenStore, _profileRefresh);
    }

    [Fact]
    public async Task InitializeAsync_Should_PopulateWelcomeLine_From_UserProfileApi_When_Available()
    {
        // Arrange
        HomeViewModel sut = CreateSut(ValidJwtWithName);
        UserScopeDto userScope = new() { UserId = 42, Name = "Max Mustermann", Email = "max@test.de" };
        _userApiMock.Setup(u => u.GetUserProfileAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserScopeDto>.Success(userScope));

        // Act
        await sut.InitializeAsync();

        // Assert
        Assert.Equal("Willkommen Max Mustermann.", sut.WelcomeLine);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task InitializeAsync_Should_FallbackToJwtDisplayName_When_ApiFails()
    {
        // Arrange
        HomeViewModel sut = CreateSut(ValidJwtWithName);
        _userApiMock.Setup(u => u.GetUserProfileAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserScopeDto>.Failure("Network error"));

        // Act
        await sut.InitializeAsync();

        // Assert
        Assert.Equal("Willkommen someuser.", sut.WelcomeLine);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task InitializeAsync_Should_SetWelcomeLineNull_When_NoToken()
    {
        // Arrange
        HomeViewModel sut = CreateSut(null);

        // Act
        await sut.InitializeAsync();

        // Assert
        Assert.Null(sut.WelcomeLine);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task ProfileRefresh_Should_ReloadWelcomeGreeting()
    {
        // Arrange
        HomeViewModel sut = CreateSut(ValidJwtWithName);
        UserScopeDto initialScope = new() { UserId = 42, Name = "Old Name" };
        UserScopeDto updatedScope = new() { UserId = 42, Name = "New Name" };

        _userApiMock.SetupSequence(u => u.GetUserProfileAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserScopeDto>.Success(initialScope))
            .ReturnsAsync(ApiResult<UserScopeDto>.Success(updatedScope));

        await sut.InitializeAsync();
        Assert.Equal("Willkommen Old Name.", sut.WelcomeLine);

        // Act
        await _profileRefresh.NotifyAsync();

        // Assert
        Assert.Equal("Willkommen New Name.", sut.WelcomeLine);
    }
}
