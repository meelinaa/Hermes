using Blazored.LocalStorage;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.NewsService;
using Hermes.WebFrontend.Client.Services.User;
using Hermes.WebFrontend.Client.Tests.Infrastructure;
using Hermes.WebFrontend.Client.ViewModels;
using Moq;
using Xunit;

using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Hermes.WebFrontend.Client.Tests.ViewModels;

/// <summary>
/// Unit tests verifying profile state manipulation, validation, and modal workflows in <see cref="UserSettingsViewModel"/>.
/// </summary>
using Hermes.WebFrontend.Client.Services.Notifications;

public sealed class UserSettingsViewModelTests
{
    private readonly Mock<IUserApiClient> _userApiMock = new();
    private readonly Mock<ILocalStorageService> _localStorageMock = new();
    private readonly UserProfileRefreshStore _profileRefresh = new();
    private readonly TestNavigationManager _navManager = new();
    private readonly Mock<IToastNotificationService> _toastMock = new();

    private UserSettingsViewModel CreateSut()
    {
        AuthTokenStore tokenStore = new(_localStorageMock.Object);
        NewsSubscriptionApiClient newsClient = new(new Mock<ILogger<NewsSubscriptionApiClient>>().Object);
        Mock<IJSRuntime> jsMock = new();
        Mock<ILogger<AuthLogoutService>> loggerMock = new();
        AuthLogoutService logoutService = new(new HttpClient(), tokenStore, newsClient, jsMock.Object, _navManager, loggerMock.Object);
        return new UserSettingsViewModel(_userApiMock.Object, tokenStore, _profileRefresh, logoutService, _toastMock.Object);
    }

    [Fact]
    public async Task LoadProfileAsync_Should_PopulateProperties_When_ApiSucceeds()
    {
        // Arrange
        UserSettingsViewModel sut = CreateSut();
        typeof(UserSettingsViewModel).GetProperty("ProfileUserId")!.SetValue(sut, 5);

        UserScopeDto userDto = new() { UserId = 5, Email = "user@test.de", Name = "Test User", IsEmailVerified = true };
        _userApiMock.Setup(u => u.GetUserProfileAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserScopeDto>.Success(userDto));

        // Act
        await sut.LoadProfileAsync();

        // Assert
        Assert.Equal("Test User", sut.ProfileName);
        Assert.Equal("user@test.de", sut.ProfileEmail);
        Assert.True(sut.ProfileEmailVerified);
        Assert.Null(sut.ProfileFeedback);
    }

    [Fact]
    public async Task UpdateProfileAsync_Should_Reject_When_NewPasswordViolatesComplexity()
    {
        // Arrange
        UserSettingsViewModel sut = CreateSut();
        typeof(UserSettingsViewModel).GetProperty("ProfileUserId")!.SetValue(sut, 5);
        sut.ProfileName = "Name";
        sut.ProfileEmail = "mail@test.de";
        sut.OldPassword = "CurrentPassword123!";
        sut.NewPassword = "weak";

        // Act
        bool success = await sut.UpdateProfileAsync();

        // Assert
        Assert.False(success);
        Assert.Contains("erfüllt nicht alle Anforderungen", sut.ProfileFeedback);
        _userApiMock.Verify(u => u.UpdateUserAsync(It.IsAny<UserProfileUpdateRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_Should_SetOldPasswordFieldError_When_CurrentPasswordIsWrong()
    {
        // Arrange
        UserSettingsViewModel sut = CreateSut();
        typeof(UserSettingsViewModel).GetProperty("ProfileUserId")!.SetValue(sut, 5);
        sut.ProfileName = "Name";
        sut.ProfileEmail = "mail@test.de";
        sut.OldPassword = "WrongCurrentPassword123!";
        sut.NewPassword = "NewValidPassword123!";

        _userApiMock.Setup(u => u.UpdateUserAsync(It.IsAny<UserProfileUpdateRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserScopeDto>.Failure(
                "Das aktuelle Passwort ist falsch.",
                problemType: HermesApiProblemTypeConstants.WRONG_CURRENT_PASSWORD));

        // Act
        bool success = await sut.UpdateProfileAsync();

        // Assert
        Assert.False(success);
        Assert.Equal("Das aktuelle Passwort ist falsch.", sut.OldPasswordFieldError);
        Assert.Null(sut.ProfileFeedback);
    }

    [Fact]
    public async Task UpdateProfileAsync_Should_Succeed_When_Valid()
    {
        // Arrange
        UserSettingsViewModel sut = CreateSut();
        typeof(UserSettingsViewModel).GetProperty("ProfileUserId")!.SetValue(sut, 5);
        sut.ProfileName = "Updated Name";
        sut.ProfileEmail = "updated@test.de";
        sut.OldPassword = "OldPassword123!";
        sut.NewPassword = "NewPassword123!";

        UserScopeDto updatedScope = new() { UserId = 5, Email = "updated@test.de", Name = "Updated Name", IsEmailVerified = true };
        _userApiMock.Setup(u => u.UpdateUserAsync(It.IsAny<UserProfileUpdateRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserScopeDto>.Success(updatedScope));

        // Act
        bool success = await sut.UpdateProfileAsync();

        // Assert
        Assert.True(success);
        Assert.Equal("Profil gespeichert.", sut.ProfileFeedback);
        Assert.Equal(string.Empty, sut.OldPassword);
        Assert.Equal(string.Empty, sut.NewPassword);
    }

    [Fact]
    public async Task ConfirmVerificationCodeAsync_Should_VerifyEmail_AndCloseModal()
    {
        // Arrange
        UserSettingsViewModel sut = CreateSut();
        typeof(UserSettingsViewModel).GetProperty("ProfileUserId")!.SetValue(sut, 5);
        sut.ShowVerificationModal = true;
        sut.VerificationCodeInput = "123456";

        UserScopeDto verifiedScope = new() { UserId = 5, Email = "verified@test.de", Name = "Name", IsEmailVerified = true };
        _userApiMock.Setup(u => u.ConfirmEmailVerificationCodeAsync(5, "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<UserScopeDto>.Success(verifiedScope));

        // Act
        bool success = await sut.ConfirmVerificationCodeAsync();

        // Assert
        Assert.True(success);
        Assert.False(sut.ShowVerificationModal);
        Assert.True(sut.ProfileEmailVerified);
        Assert.Equal("E-Mail-Adresse wurde verifiziert.", sut.ProfileFeedback);
    }

    [Fact]
    public async Task ConfirmDeleteAccountAsync_Should_DeleteAccount_AndTriggerLogout()
    {
        // Arrange
        UserSettingsViewModel sut = CreateSut();
        typeof(UserSettingsViewModel).GetProperty("ProfileUserId")!.SetValue(sut, 5);
        sut.ShowDeleteAccountModal = true;

        _userApiMock.Setup(u => u.DeleteAccountAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult.Success());

        // Act
        bool success = await sut.ConfirmDeleteAccountAsync();

        // Assert
        Assert.True(success);
        Assert.False(sut.ShowDeleteAccountModal);
        _userApiMock.Verify(u => u.DeleteAccountAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }
}
