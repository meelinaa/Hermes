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
/// Unit tests verifying live validation rules and registration flows in <see cref="RegisterViewModel"/>.
/// </summary>
public sealed class RegisterViewModelTests
{
    private readonly Mock<IAuthApiClient> _authApiMock = new();
    private readonly Mock<ILocalStorageService> _localStorageMock = new();
    private readonly TestNavigationManager _navManager = new();
    private readonly Mock<IToastNotificationService> _toastMock = new();

    private RegisterViewModel CreateSut()
    {
        AuthTokenStore store = new(_localStorageMock.Object);
        return new RegisterViewModel(_authApiMock.Object, store, _navManager, _toastMock.Object);
    }

    [Theory]
    [InlineData("short", false, false, false, false)]
    [InlineData("lowercase1!", true, false, true, true)]
    [InlineData("UPPERCASE1!", true, false, true, true)]
    [InlineData("NoDigitsSymbol!", true, true, false, true)]
    [InlineData("ValidPassword123!", true, true, true, true)]
    public void PasswordRules_Should_EvaluateExpectedComplexity(
        string password,
        bool expectLength,
        bool expectCase,
        bool expectDigit,
        bool expectSymbol)
    {
        // Arrange
        RegisterViewModel sut = CreateSut();
        sut.Password = password;

        // Assert
        Assert.Equal(expectLength, sut.PasswordLenOk);
        Assert.Equal(expectCase, sut.PasswordCaseOk);
        Assert.Equal(expectDigit, sut.PasswordDigitOk);
        Assert.Equal(expectSymbol, sut.PasswordSymbolOk);
    }

    [Theory]
    [InlineData("valid@domain.com", true)]
    [InlineData("not-an-email", false)]
    [InlineData("", false)]
    public void EmailFormat_Should_ValidateFormat(string email, bool expectedValid)
    {
        // Arrange
        RegisterViewModel sut = CreateSut();
        sut.Email = email;

        // Assert
        Assert.Equal(expectedValid, sut.EmailFormatOk);
    }

    [Fact]
    public async Task RegisterAsync_Should_PreventExecution_When_FormInvalid()
    {
        // Arrange
        RegisterViewModel sut = CreateSut();
        sut.Username = "user";
        sut.Email = "invalid-email";
        sut.Password = "weak";

        // Act
        bool success = await sut.RegisterAsync();

        // Assert
        Assert.False(success);
        Assert.False(sut.IsBusy);
        _authApiMock.Verify(a => a.RegisterAsync(It.IsAny<RegisterRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_Should_SetError_When_RegistrationApiFails()
    {
        // Arrange
        RegisterViewModel sut = CreateSut();
        sut.Username = "user";
        sut.Email = "taken@domain.com";
        sut.Password = "StrongPassword123!";

        _authApiMock.Setup(a => a.RegisterAsync(It.IsAny<RegisterRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserScopeDto>.Failure("Diese E-Mail ist bereits registriert."));

        // Act
        bool success = await sut.RegisterAsync();

        // Assert
        Assert.False(success);
        Assert.Equal("Diese E-Mail ist bereits registriert.", sut.RegisterError);
        _authApiMock.Verify(a => a.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_Should_AutoLoginAndRedirect_When_RegistrationSucceeds()
    {
        // Arrange
        RegisterViewModel sut = CreateSut();
        sut.Username = "newuser";
        sut.Email = "new@domain.com";
        sut.Password = "StrongPassword123!";

        UserScopeDto userScope = new() { UserId = 10, Email = "new@domain.com", Name = "newuser", IsEmailVerified = true };
        LoginResponseDto loginResponse = new() { AccessToken = "token-a", RefreshToken = "token-r", UserId = 10, Success = true };

        _authApiMock.Setup(a => a.RegisterAsync(It.IsAny<RegisterRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserScopeDto>.Success(userScope));
        _authApiMock.Setup(a => a.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<LoginResponseDto>.Success(loginResponse));

        // Act
        bool success = await sut.RegisterAsync();

        // Assert
        Assert.True(success);
        Assert.Null(sut.RegisterError);
        Assert.Equal("http://localhost/home", _navManager.Uri);
        _localStorageMock.Verify(s => s.SetItemAsync("hermes.auth.accessToken", "token-a", It.IsAny<CancellationToken>()), Times.Once);
        _localStorageMock.Verify(s => s.SetItemAsync("hermes.auth.refreshToken", "token-r", It.IsAny<CancellationToken>()), Times.Once);
        _toastMock.Verify(t => t.ShowSuccess("Konto erfolgreich erstellt! Willkommen bei Hermes.", "Registrierung", 4000), Times.Once);
    }
}
