using Blazored.LocalStorage;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.Model;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.Notifications;
using Hermes.WebFrontend.Client.Tests.Infrastructure;
using Hermes.WebFrontend.Client.ViewModels;
using Moq;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.ViewModels;

/// <summary>
/// Unit tests verifying state management and execution flows in <see cref="LoginViewModel"/>.
/// </summary>
public sealed class LoginViewModelTests
{
    private readonly Mock<IAuthApiClient> _authApiMock = new();
    private readonly Mock<ILocalStorageService> _localStorageMock = new();
    private readonly TestNavigationManager _navManager = new();
    private readonly Mock<IToastNotificationService> _toastMock = new();

    private LoginViewModel CreateSut(AuthTokenStore? tokenStore = null)
    {
        AuthTokenStore store = tokenStore ?? new AuthTokenStore(_localStorageMock.Object);
        return new LoginViewModel(_authApiMock.Object, store, _navManager, _toastMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_Should_RedirectToHome_When_TokenAlreadyExists()
    {
        // Arrange
        _localStorageMock.Setup(s => s.GetItemAsync<string>("hermes.auth.accessToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync("existing-jwt-token");
        AuthTokenStore store = new(_localStorageMock.Object);
        LoginViewModel sut = CreateSut(store);

        // Act
        await sut.InitializeAsync();

        // Assert
        Assert.Equal("http://localhost/home", _navManager.Uri);
    }

    [Fact]
    public void TogglePasswordVisibility_Should_InvertVisibility()
    {
        // Arrange
        LoginViewModel sut = CreateSut();
        Assert.False(sut.ShowLoginPassword);

        // Act & Assert
        sut.TogglePasswordVisibility();
        Assert.True(sut.ShowLoginPassword);
        sut.TogglePasswordVisibility();
        Assert.False(sut.ShowLoginPassword);
    }

    [Theory]
    [InlineData("", "Secret123!")]
    [InlineData("user", "")]
    [InlineData("   ", "   ")]
    public async Task LoginAsync_Should_FailValidation_When_CredentialsIncomplete(string username, string password)
    {
        // Arrange
        LoginViewModel sut = CreateSut();
        sut.UserName = username;
        sut.Password = password;

        // Act
        bool success = await sut.LoginAsync();

        // Assert
        Assert.False(success);
        Assert.Equal("Bitte Benutzername und Passwort eingeben.", sut.LoginError);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task LoginAsync_Should_SetError_When_ApiFails()
    {
        // Arrange
        LoginViewModel sut = CreateSut();
        sut.UserName = "tester";
        sut.Password = "wrongpass";

        _authApiMock.Setup(a => a.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<LoginResponseDto>.Failure("Benutzername oder Passwort ist falsch."));

        // Act
        bool success = await sut.LoginAsync();

        // Assert
        Assert.False(success);
        Assert.Equal("Benutzername oder Passwort ist falsch.", sut.LoginError);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task LoginAsync_Should_PersistTokensAndRedirect_When_Successful()
    {
        // Arrange
        LoginViewModel sut = CreateSut();
        sut.UserName = "validuser";
        sut.Password = "Secret123!";

        LoginResponseDto loginResponse = new() { AccessToken = "new-access-token", RefreshToken = "new-refresh-token", UserId = 1, Success = true };
        _authApiMock.Setup(a => a.LoginAsync(It.Is<LoginRequestDto>(r => r.NameOrEmail == "validuser" && r.Password == "Secret123!"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<LoginResponseDto>.Success(loginResponse));

        // Act
        bool success = await sut.LoginAsync();

        // Assert
        Assert.True(success);
        Assert.Null(sut.LoginError);
        Assert.Equal("http://localhost/home", _navManager.Uri);
        _localStorageMock.Verify(s => s.SetItemAsync("hermes.auth.accessToken", "new-access-token", It.IsAny<CancellationToken>()), Times.Once);
        _localStorageMock.Verify(s => s.SetItemAsync("hermes.auth.refreshToken", "new-refresh-token", It.IsAny<CancellationToken>()), Times.Once);
        _toastMock.Verify(t => t.ShowSuccess("Erfolgreich angemeldet. Willkommen zurück!", "Login", 4000), Times.Once);
    }
}
