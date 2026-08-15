using System.Globalization;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.User;

namespace Hermes.WebFrontend.Client.ViewModels;

/// <summary>
/// ViewModel orchestrating user profile management, password updates, email verification modals, and account deletion workflows.
/// </summary>
public sealed class UserSettingsViewModel(
    IUserApiClient userApi,
    AuthTokenStore authTokens,
    UserProfileRefreshStore profileRefresh,
    AuthLogoutService logoutService) : IAsyncDisposable
{
    private const int RESEND_COOLDOWN_TOTAL_SECONDS = 180;
    private const string WRONG_OR_EXPIRED_CODE_MESSAGE =
        "Der eingegebene Code ist nicht richtig oder nicht mehr gültig. Bitte prüfen Sie den Code und versuchen Sie es erneut. " +
        "Wenn der Code abgelaufen ist oder weiterhin nicht funktioniert, können Sie nach Ablauf der Wartezeit unten eine neue Verifizierungs-E-Mail anfordern.";

    private Timer? _resendCooldownTimer;

    /// <summary>Notifies bound views when internal state or timers change.</summary>
    public event Action? StateChanged;

    /// <summary>Gets the numeric user ID.</summary>
    public int? ProfileUserId { get; private set; }

    /// <summary>Gets or sets the user's display name.</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the user's email address.</summary>
    public string ProfileEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the user's email address has been verified.</summary>
    public bool ProfileEmailVerified { get; set; }

    private string _oldPassword = string.Empty;

    /// <summary>Gets or sets the current password for verification during updates.</summary>
    public string OldPassword
    {
        get => _oldPassword;
        set
        {
            string v = value ?? string.Empty;
            if (string.Equals(_oldPassword, v, StringComparison.Ordinal))
                return;
            _oldPassword = v;
            OldPasswordFieldError = null;
        }
    }

    /// <summary>Gets or sets field-specific validation error for current password.</summary>
    public string? OldPasswordFieldError { get; set; }

    /// <summary>Gets or sets the new password string.</summary>
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the old password characters are revealed in plain text.</summary>
    public bool ShowOldPassword { get; set; }

    /// <summary>Gets or sets whether the new password characters are revealed in plain text.</summary>
    public bool ShowNewPassword { get; set; }

    /// <summary>Gets or sets whether the old password input is temporarily readonly to prevent browser autofill.</summary>
    public bool OldPasswordReadonly { get; set; } = true;

    /// <summary>Gets or sets whether profile saving is currently in flight.</summary>
    public bool ProfileBusy { get; set; }

    /// <summary>Gets or sets general profile operation feedback displayed to the user.</summary>
    public string? ProfileFeedback { get; set; }

    /// <summary>Gets whether new password meets minimum length requirement.</summary>
    public bool PasswordLenOk => NewPassword.Length >= 8;

    /// <summary>Gets whether new password contains uppercase and lowercase characters.</summary>
    public bool PasswordCaseOk => NewPassword.Any(char.IsUpper) && NewPassword.Any(char.IsLower);

    /// <summary>Gets whether new password contains at least one digit.</summary>
    public bool PasswordDigitOk => NewPassword.Any(char.IsDigit);

    /// <summary>Gets whether new password contains at least one punctuation or symbol character.</summary>
    public bool PasswordSymbolOk => NewPassword.Any(static c => char.IsPunctuation(c) || char.IsSymbol(c));

    /// <summary>Gets whether all new password complexity rules are satisfied.</summary>
    public bool NewPasswordRulesSatisfied => PasswordLenOk && PasswordCaseOk && PasswordDigitOk && PasswordSymbolOk;

    /// <summary>Gets whether the user has entered characters in the new password field.</summary>
    public bool IsAttemptingPasswordChange => !string.IsNullOrWhiteSpace(NewPassword);

    /// <summary>Gets whether the form passes all requirements to submit profile updates.</summary>
    public bool CanSaveProfile =>
        ProfileUserId is not null
        && !ProfileBusy
        && !string.IsNullOrWhiteSpace(ProfileName)
        && !string.IsNullOrWhiteSpace(ProfileEmail)
        && (!IsAttemptingPasswordChange
            || (!string.IsNullOrWhiteSpace(OldPassword) && NewPasswordRulesSatisfied));

    /// <summary>Gets whether submit button should visually appear inactive.</summary>
    public bool ProfileSubmitLooksInactive => !CanSaveProfile && !ProfileBusy;

    /// <summary>Gets the CSS class list for the profile save button.</summary>
    public string ProfileSubmitButtonClass =>
        "user-profile-submit" + (ProfileSubmitLooksInactive ? " user-profile-submit--inactive" : string.Empty);

    /// <summary>Gets the aria-disabled state for the profile submit button.</summary>
    public bool ProfileSubmitAriaDisabled => ProfileSubmitLooksInactive || ProfileBusy;

    /// <summary>Gets or sets whether the email verification modal dialog is open.</summary>
    public bool ShowVerificationModal { get; set; }

    /// <summary>Gets or sets whether the account deletion confirmation modal dialog is open.</summary>
    public bool ShowDeleteAccountModal { get; set; }

    /// <summary>Gets or sets whether account deletion is currently executing.</summary>
    public bool DeleteAccountBusy { get; set; }

    /// <summary>Gets or sets account deletion error message.</summary>
    public string? DeleteAccountError { get; set; }

    /// <summary>Gets or sets whether the verification modal is opening and initial OTP dispatch is pending.</summary>
    public bool ModalOpenBusy { get; set; }

    /// <summary>Gets or sets whether a verification OTP resend is in flight.</summary>
    public bool ModalSendBusy { get; set; }

    /// <summary>Gets or sets whether OTP code verification is in flight.</summary>
    public bool ConfirmCodeBusy { get; set; }

    /// <summary>Gets or sets the verification code typed by the user.</summary>
    public string VerificationCodeInput { get; set; } = string.Empty;

    /// <summary>Gets or sets verification code validation errors.</summary>
    public string? VerificationCodeError { get; set; }

    /// <summary>Gets or sets verification email dispatch errors.</summary>
    public string? VerificationSendError { get; set; }

    /// <summary>Gets or sets the seconds remaining before resending verification email is permitted.</summary>
    public int ResendCooldownSecondsRemaining { get; set; }

    /// <summary>
    /// Initializes the ViewModel, extracts the user ID from token storage, and fetches user details.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await authTokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
        int? userId = authTokens.AccessToken.TryGetUserId();
        if (userId is null)
        {
            ProfileFeedback = "Benutzer konnte nicht ermittelt werden. Bitte erneut anmelden.";
            NotifyStateChanged();
            return;
        }

        ProfileUserId = userId.Value;
        await LoadProfileAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches profile information from the API and populates bound fields.
    /// </summary>
    public async Task LoadProfileAsync(CancellationToken cancellationToken = default)
    {
        if (ProfileUserId is null)
            return;

        ApiResult<UserScopeDto> result = await userApi.GetUserProfileAsync(ProfileUserId.Value, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess && result.Value is not null)
        {
            ProfileName = result.Value.Name ?? string.Empty;
            ProfileEmail = result.Value.Email ?? string.Empty;
            ProfileEmailVerified = result.Value.IsEmailVerified;
        }
        else
        {
            ProfileFeedback = result.ErrorMessage;
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// Updates the user profile name, email, and password via the API.
    /// </summary>
    public async Task<bool> UpdateProfileAsync(CancellationToken cancellationToken = default)
    {
        ProfileFeedback = null;
        OldPasswordFieldError = null;

        if (ProfileUserId is null)
        {
            ProfileFeedback = "Benutzer konnte nicht ermittelt werden. Bitte erneut anmelden.";
            NotifyStateChanged();
            return false;
        }

        if (string.IsNullOrWhiteSpace(ProfileName) || string.IsNullOrWhiteSpace(ProfileEmail))
        {
            ProfileFeedback = "Name und E-Mail dürfen nicht leer sein.";
            NotifyStateChanged();
            return false;
        }

        string? newPw = string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword;
        string? curPw = string.IsNullOrWhiteSpace(OldPassword) ? null : OldPassword;
        if (newPw is not null && curPw is null)
        {
            ProfileFeedback = "Bitte das aktuelle Passwort eingeben, wenn du ein neues Passwort setzen möchtest.";
            NotifyStateChanged();
            return false;
        }

        if (newPw is not null && !NewPasswordRulesSatisfied)
        {
            ProfileFeedback = "Das neue Passwort erfüllt nicht alle Anforderungen: mindestens 8 Zeichen, Groß- und Kleinbuchstaben, mindestens eine Zahl und ein Symbol.";
            NotifyStateChanged();
            return false;
        }

        ProfileBusy = true;
        NotifyStateChanged();

        try
        {
            UserProfileUpdateRequestDto request = new()
            {
                Id = ProfileUserId.Value,
                Name = ProfileName.Trim(),
                Email = ProfileEmail.Trim(),
                NewPassword = newPw,
                CurrentPassword = curPw
            };

            ApiResult<UserScopeDto> result = await userApi.UpdateUserAsync(request, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                if (newPw is not null && string.Equals(result.ProblemType, HermesApiProblemTypeConstants.WRONG_CURRENT_PASSWORD, StringComparison.Ordinal))
                {
                    OldPasswordFieldError = result.ErrorMessage;
                    ProfileFeedback = null;
                }
                else
                {
                    OldPasswordFieldError = null;
                    ProfileFeedback = result.ErrorMessage;
                }

                return false;
            }

            OldPassword = string.Empty;
            NewPassword = string.Empty;
            ShowOldPassword = false;
            ShowNewPassword = false;
            OldPasswordReadonly = true;
            ProfileFeedback = "Profil gespeichert.";

            if (result.Value is not null)
            {
                ProfileName = result.Value.Name ?? ProfileName;
                ProfileEmail = result.Value.Email ?? ProfileEmail;
                ProfileEmailVerified = result.Value.IsEmailVerified;
            }

            await profileRefresh.NotifyAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            ProfileBusy = false;
            NotifyStateChanged();
        }
    }

    /// <summary>Removes readonly attribute from current password input on first user focus.</summary>
    public void OnOldPasswordFocus()
    {
        if (!OldPasswordReadonly)
            return;
        OldPasswordReadonly = false;
        NotifyStateChanged();
    }

    /// <summary>Opens the email verification dialog and sends an initial verification code.</summary>
    public async Task OpenEmailVerificationModalAsync(CancellationToken cancellationToken = default)
    {
        ProfileFeedback = null;
        VerificationCodeError = null;
        VerificationSendError = null;
        VerificationCodeInput = string.Empty;

        if (ProfileUserId is null)
        {
            ProfileFeedback = "Benutzer konnte nicht ermittelt werden. Bitte erneut anmelden.";
            NotifyStateChanged();
            return;
        }

        if (string.IsNullOrWhiteSpace(ProfileEmail))
        {
            ProfileFeedback = "Bitte zuerst eine E-Mail-Adresse eintragen.";
            NotifyStateChanged();
            return;
        }

        ModalOpenBusy = true;
        ShowVerificationModal = true;
        StopResendCooldownTimer();
        ResendCooldownSecondsRemaining = 0;
        NotifyStateChanged();

        try
        {
            bool ok = await SendVerificationEmailAsync(cancellationToken).ConfigureAwait(false);
            if (ok)
            {
                ResendCooldownSecondsRemaining = RESEND_COOLDOWN_TOTAL_SECONDS;
                ArmResendCooldownTimer();
            }
        }
        finally
        {
            ModalOpenBusy = false;
            NotifyStateChanged();
        }
    }

    /// <summary>Closes the email verification dialog and resets timer.</summary>
    public void CloseEmailVerificationModal()
    {
        StopResendCooldownTimer();
        ResendCooldownSecondsRemaining = 0;
        ShowVerificationModal = false;
        VerificationCodeInput = string.Empty;
        VerificationCodeError = null;
        VerificationSendError = null;
        NotifyStateChanged();
    }

    /// <summary>Resends a fresh verification OTP email when cooldown has expired.</summary>
    public async Task OnResendVerificationEmailAsync(CancellationToken cancellationToken = default)
    {
        if (ResendCooldownSecondsRemaining > 0 || ModalSendBusy || !ShowVerificationModal)
            return;

        VerificationSendError = null;
        VerificationCodeError = null;
        ModalSendBusy = true;
        NotifyStateChanged();

        try
        {
            bool ok = await SendVerificationEmailAsync(cancellationToken).ConfigureAwait(false);
            if (ok)
            {
                ResendCooldownSecondsRemaining = RESEND_COOLDOWN_TOTAL_SECONDS;
                ArmResendCooldownTimer();
            }
        }
        finally
        {
            ModalSendBusy = false;
            NotifyStateChanged();
        }
    }

    /// <summary>Dispatches OTP verification email request to the backend API.</summary>
    public async Task<bool> SendVerificationEmailAsync(CancellationToken cancellationToken = default)
    {
        if (ProfileUserId is null)
        {
            VerificationSendError = "Benutzer konnte nicht ermittelt werden.";
            NotifyStateChanged();
            return false;
        }

        ApiResult<SendVerificationMailResponseDto> result = await userApi.SendEmailVerificationCodeAsync(ProfileUserId.Value, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            VerificationSendError = result.ErrorMessage;
            NotifyStateChanged();
            return false;
        }

        VerificationSendError = null;
        NotifyStateChanged();
        return true;
    }

    /// <summary>Submits the entered verification OTP code to confirm email ownership.</summary>
    public async Task<bool> ConfirmVerificationCodeAsync(CancellationToken cancellationToken = default)
    {
        VerificationCodeError = null;
        if (ProfileUserId is null)
        {
            VerificationCodeError = "Benutzer konnte nicht ermittelt werden.";
            NotifyStateChanged();
            return false;
        }

        string code = VerificationCodeInput.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            VerificationCodeError = "Bitte den Code eingeben.";
            NotifyStateChanged();
            return false;
        }

        ConfirmCodeBusy = true;
        NotifyStateChanged();

        try
        {
            ApiResult<UserScopeDto> result = await userApi.ConfirmEmailVerificationCodeAsync(ProfileUserId.Value, code, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                VerificationCodeError = result.StatusCode is 400 or 404 or 422
                    ? WRONG_OR_EXPIRED_CODE_MESSAGE
                    : result.ErrorMessage;
                return false;
            }

            VerificationCodeError = null;
            StopResendCooldownTimer();
            ResendCooldownSecondsRemaining = 0;
            ShowVerificationModal = false;
            VerificationCodeInput = string.Empty;
            ProfileFeedback = "E-Mail-Adresse wurde verifiziert.";

            if (result.Value is not null)
            {
                ProfileName = result.Value.Name ?? ProfileName;
                ProfileEmail = result.Value.Email ?? ProfileEmail;
                ProfileEmailVerified = result.Value.IsEmailVerified;
            }

            await profileRefresh.NotifyAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            ConfirmCodeBusy = false;
            NotifyStateChanged();
        }
    }

    /// <summary>Opens the account deletion confirmation modal.</summary>
    public void OpenDeleteAccountModal()
    {
        DeleteAccountError = null;
        ShowDeleteAccountModal = true;
        NotifyStateChanged();
    }

    /// <summary>Closes the account deletion confirmation modal.</summary>
    public void CloseDeleteAccountModal()
    {
        if (DeleteAccountBusy)
            return;
        ShowDeleteAccountModal = false;
        DeleteAccountError = null;
        NotifyStateChanged();
    }

    /// <summary>Executes permanent deletion of the user account and signs out.</summary>
    public async Task<bool> ConfirmDeleteAccountAsync(CancellationToken cancellationToken = default)
    {
        if (ProfileUserId is null)
        {
            DeleteAccountError = "Benutzer konnte nicht ermittelt werden. Bitte erneut anmelden.";
            NotifyStateChanged();
            return false;
        }

        DeleteAccountError = null;
        DeleteAccountBusy = true;
        NotifyStateChanged();

        try
        {
            ApiResult result = await userApi.DeleteAccountAsync(ProfileUserId.Value, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                DeleteAccountError = result.ErrorMessage;
                return false;
            }

            ShowDeleteAccountModal = false;
            await logoutService.SignOutAndReloadAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            DeleteAccountBusy = false;
            NotifyStateChanged();
        }
    }

    /// <summary>Formats remaining seconds into mm:ss display string.</summary>
    public static string FormatMmSs(int totalSeconds)
    {
        int m = totalSeconds / 60;
        int s = totalSeconds % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{m}:{s:D2}");
    }

    private void ArmResendCooldownTimer()
    {
        StopResendCooldownTimer();
        _resendCooldownTimer = new Timer(
            _ =>
            {
                if (!ShowVerificationModal)
                {
                    StopResendCooldownTimer();
                    return;
                }

                if (ResendCooldownSecondsRemaining > 0)
                    ResendCooldownSecondsRemaining--;

                if (ResendCooldownSecondsRemaining <= 0)
                    StopResendCooldownTimer();

                NotifyStateChanged();
            },
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    private void StopResendCooldownTimer()
    {
        _resendCooldownTimer?.Dispose();
        _resendCooldownTimer = null;
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    /// <summary>Disposes timers and resources asynchronously.</summary>
    public async ValueTask DisposeAsync()
    {
        StopResendCooldownTimer();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
