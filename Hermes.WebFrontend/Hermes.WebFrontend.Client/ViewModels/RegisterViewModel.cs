using System.ComponentModel.DataAnnotations;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.Model;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.Notifications;
using Microsoft.AspNetCore.Components;

namespace Hermes.WebFrontend.Client.ViewModels;

/// <summary>
/// ViewModel managing account registration inputs, live password validation rules, and automatic login after signup.
/// </summary>
public sealed class RegisterViewModel(
    IAuthApiClient authApi,
    AuthTokenStore tokenStore,
    NavigationManager navigation,
    IToastNotificationService toastService)
{
    private static readonly EmailAddressAttribute _emailValidator = new();

    /// <summary>Gets or sets the desired username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the user email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Gets or sets the chosen plain-text password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets error feedback displayed to the user.</summary>
    public string? RegisterError { get; set; }

    /// <summary>Gets or sets whether a registration request is currently being processed.</summary>
    public bool IsBusy { get; set; }

    /// <summary>Checks whether the password meets the minimum 8-character length rule.</summary>
    public bool PasswordLenOk => Password.Length >= 8;

    /// <summary>Checks whether the password contains both uppercase and lowercase letters.</summary>
    public bool PasswordCaseOk => Password.Any(char.IsUpper) && Password.Any(char.IsLower);

    /// <summary>Checks whether the password contains at least one numeric digit.</summary>
    public bool PasswordDigitOk => Password.Any(char.IsDigit);

    /// <summary>Checks whether the password contains at least one special character or symbol.</summary>
    public bool PasswordSymbolOk => Password.Any(static c => char.IsPunctuation(c) || char.IsSymbol(c));

    /// <summary>Evaluates whether all four password complexity rules are satisfied.</summary>
    public bool PasswordRulesSatisfied => PasswordLenOk && PasswordCaseOk && PasswordDigitOk && PasswordSymbolOk;

    /// <summary>Checks whether the entered email address is formatted correctly.</summary>
    public bool EmailFormatOk => !string.IsNullOrWhiteSpace(Email) && _emailValidator.IsValid(Email.Trim());

    /// <summary>Gets whether the email format error hint should be displayed.</summary>
    public bool ShowEmailFormatError => !string.IsNullOrWhiteSpace(Email) && !EmailFormatOk;

    /// <summary>Gets whether the registration form is valid and ready for submission.</summary>
    public bool CanRegister => !string.IsNullOrWhiteSpace(Username) && EmailFormatOk && PasswordRulesSatisfied;

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
    /// Returns the CSS class for password requirement items based on their satisfied state.
    /// </summary>
    public static string RuleClass(bool ok) => ok ? "hermes-reg-rule hermes-reg-rule--ok" : "hermes-reg-rule";

    /// <summary>
    /// Validates registration details, creates the account, logs the user in, and redirects to dashboard.
    /// </summary>
    public async Task<bool> RegisterAsync(CancellationToken cancellationToken = default)
    {
        RegisterError = null;
        if (!CanRegister || IsBusy)
            return false;

        IsBusy = true;
        try
        {
            RegisterRequestDto regBody = new()
            {
                Name = Username.Trim(),
                Email = Email.Trim(),
                Password = Password,
            };

            ApiResult<UserScopeDto> regResult = await authApi.RegisterAsync(regBody, cancellationToken).ConfigureAwait(false);
            if (!regResult.IsSuccess)
            {
                RegisterError = regResult.ErrorMessage ?? "Registrierung fehlgeschlagen.";
                return false;
            }

            ApiResult<LoginResponseDto> loginResult = await authApi.LoginAsync(
                new LoginRequestDto { NameOrEmail = Email.Trim(), Password = Password },
                cancellationToken).ConfigureAwait(false);

            if (!loginResult.IsSuccess || loginResult.Value is null)
            {
                RegisterError = loginResult.ErrorMessage
                    ?? "Konto wurde angelegt; automatische Anmeldung ist fehlgeschlagen. Bitte auf der Anmeldeseite einloggen.";
                return false;
            }

            await tokenStore.PersistAsync(loginResult.Value.AccessToken, loginResult.Value.RefreshToken, cancellationToken).ConfigureAwait(false);
            toastService.ShowSuccess("Konto erfolgreich erstellt! Willkommen bei Hermes.", "Registrierung");
            navigation.NavigateTo("/home");
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
