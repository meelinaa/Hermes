using System.Globalization;
using Blazored.LocalStorage;

namespace Hermes.WebFrontend.Client.Services.Auth;

/// <summary>
/// Persists JWT access + refresh tokens in browser local storage for API calls.
/// </summary>
public sealed class AuthTokenStore(ILocalStorageService localStorage)
{
    private const string ACCESS_KEY = "hermes.auth.accessToken";
    private const string REFRESH_KEY = "hermes.auth.refreshToken";
    private const string LAST_ACTIVITY_KEY = "hermes.auth.lastActivityUtc";
    private bool _loaded;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }

    /// <summary>Last user activity in the app (UTC), for sliding idle timeout.</summary>
    public DateTimeOffset? LastActivityUtc { get; private set; }

    /// <summary>Loads tokens and session metadata from browser storage once per instance.</summary>
    public async Task EnsureLoadedFromStorageAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
            return;
        _loaded = true;
        cancellationToken.ThrowIfCancellationRequested();
        AccessToken = await localStorage.GetItemAsync<string>(ACCESS_KEY, cancellationToken).ConfigureAwait(false);
        RefreshToken = await localStorage.GetItemAsync<string>(REFRESH_KEY, cancellationToken).ConfigureAwait(false);
        string? activityRaw = await localStorage.GetItemAsync<string>(LAST_ACTIVITY_KEY, cancellationToken).ConfigureAwait(false);
        LastActivityUtc = ParseActivity(activityRaw);
    }

    /// <summary>Updates and persists the last user-activity timestamp.</summary>
    public async Task TouchActivityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastActivityUtc = DateTimeOffset.UtcNow;
        await localStorage.SetItemAsync(LAST_ACTIVITY_KEY, LastActivityUtc.Value.ToString("O", CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Persists access and refresh tokens and updates activity timestamp.</summary>
    public async Task PersistAsync(string accessToken, string refreshToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        _loaded = true;
        await localStorage.SetItemAsync(ACCESS_KEY, accessToken, cancellationToken).ConfigureAwait(false);
        await localStorage.SetItemAsync(REFRESH_KEY, refreshToken, cancellationToken).ConfigureAwait(false);
        await TouchActivityAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Clears all stored authentication and session values.</summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccessToken = null;
        RefreshToken = null;
        LastActivityUtc = null;
        _loaded = false;
        await localStorage.RemoveItemAsync(ACCESS_KEY, cancellationToken).ConfigureAwait(false);
        await localStorage.RemoveItemAsync(REFRESH_KEY, cancellationToken).ConfigureAwait(false);
        await localStorage.RemoveItemAsync(LAST_ACTIVITY_KEY, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Parses a round-trip timestamp string into UTC activity value.</summary>
    private static DateTimeOffset? ParseActivity(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset dto)
            ? dto
            : null;
    }
}
