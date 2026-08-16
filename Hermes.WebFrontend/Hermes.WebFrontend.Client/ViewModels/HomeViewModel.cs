using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.User;

namespace Hermes.WebFrontend.Client.ViewModels;

/// <summary>
/// ViewModel managing dashboard presentation state, personalized welcome greetings, and live profile change updates.
/// </summary>
public sealed class HomeViewModel(
    IUserApiClient userApi,
    AuthTokenStore authTokens,
    UserProfileRefreshStore profileRefresh) : IDisposable
{
    private Func<Task>? _profileChangedHandler;

    /// <summary>Event raised whenever the welcome message or state changes.</summary>
    public event Action? StateChanged;

    /// <summary>Gets the personalized welcome greeting string, or null if unauthenticated.</summary>
    public string? WelcomeLine { get; private set; }

    /// <summary>Gets whether the greeting data is currently being loaded.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Initializes profile change subscriptions and retrieves the personalized greeting.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _profileChangedHandler = OnProfileChangedAsync;
        profileRefresh.Subscribe(_profileChangedHandler);
        await LoadWelcomeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches the user profile or falls back to JWT claims to compose the welcome greeting.
    /// </summary>
    public async Task LoadWelcomeAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        NotifyStateChanged();

        try
        {
            await authTokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
            int? userId = authTokens.AccessToken.TryGetUserId();
            if (userId is null)
            {
                WelcomeLine = ApplyWelcomeFromJwt();
                return;
            }

            ApiResult<UserScopeDto> result = await userApi.GetUserProfileAsync(userId.Value, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value?.Name))
            {
                WelcomeLine = $"Willkommen {result.Value.Name.Trim()}.";
                return;
            }

            WelcomeLine = ApplyWelcomeFromJwt();
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    private async Task OnProfileChangedAsync()
    {
        await LoadWelcomeAsync().ConfigureAwait(false);
    }

    private string? ApplyWelcomeFromJwt()
    {
        string? name = authTokens.AccessToken.TryGetDisplayName();
        return string.IsNullOrWhiteSpace(name) ? null : $"Willkommen {name.Trim()}.";
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    /// <summary>
    /// Unsubscribes from profile change events and frees resources.
    /// </summary>
    public void Dispose()
    {
        if (_profileChangedHandler is not null)
        {
            profileRefresh.Unsubscribe(_profileChangedHandler);
            _profileChangedHandler = null;
        }
    }
}
