using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.Model;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Microsoft.AspNetCore.Components;

namespace Hermes.WebFrontend.Client.ViewModels;

/// <summary>
/// ViewModel managing authentication credentials, form state, and login submission workflows.
/// </summary>
public sealed class LoginViewModel(
    IAuthApiClient authApi,
    AuthTokenStore tokenStore,
    NavigationManager navigation)
{
    /// <summary>Gets or sets the entered username or email address.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the entered plain-text password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the password characters are revealed in plain text.</summary>
    public bool ShowLoginPassword { get; set; }

    /// <summary>Gets or sets error feedback displayed to the user.</summary>
    public string? LoginError { get; set; }

    /// <summary>Gets or sets whether an authentication request is currently in flight.</summary>
    public bool IsBusy { get; set; }

    /// <summary>
    /// Initializes the view state and redirects already authenticated users to the home dashboard.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await tokenStore.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(tokenStore.AccessToken))
            navigation.NavigateTo("/home", forceLoad: false, replace: true);
    }

    /// <summary>
    /// Toggles the plain-text password visibility flag.
    /// </summary>
    public void TogglePasswordVisibility() => ShowLoginPassword = !ShowLoginPassword;

    /// <summary>
    /// Validates login inputs and submits authentication credentials to the API.
    /// </summary>
    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
    {
        LoginError = null;
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            LoginError = "Bitte Benutzername und Passwort eingeben.";
            return false;
        }

        IsBusy = true;
        try
        {
            ApiResult<LoginResponseDto> result = await authApi.LoginAsync(
                new LoginRequestDto { NameOrEmail = UserName.Trim(), Password = Password },
                cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess || result.Value is null)
            {
                LoginError = result.ErrorMessage ?? "Anmeldung fehlgeschlagen.";
                return false;
            }

            await tokenStore.PersistAsync(result.Value.AccessToken, result.Value.RefreshToken, cancellationToken).ConfigureAwait(false);
            navigation.NavigateTo("/home");
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
