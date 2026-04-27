using System.Globalization;
using Blazored.LocalStorage;

namespace Hermes.WebFrontend.Client.Services.Auth;

/// <summary>
/// Persists JWT access + refresh tokens in browser local storage for API calls.
/// </summary>
public sealed class AuthTokenStore(ILocalStorageService localStorage)
{
    private const string AccessKey = "hermes.auth.accessToken";
    private const string RefreshKey = "hermes.auth.refreshToken";
    private const string LastActivityKey = "hermes.auth.lastActivityUtc";
    private bool _loaded;
    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset? _lastActivityUtc;

    public string? AccessToken => _accessToken;
    public string? RefreshToken => _refreshToken;

    /// <summary>Last user activity in the app (UTC), for sliding idle timeout.</summary>
    public DateTimeOffset? LastActivityUtc => _lastActivityUtc;

    public async Task EnsureLoadedFromStorageAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
            return;
        _loaded = true;
        cancellationToken.ThrowIfCancellationRequested();
        _accessToken = await localStorage.GetItemAsync<string>(AccessKey, cancellationToken).ConfigureAwait(false);
        _refreshToken = await localStorage.GetItemAsync<string>(RefreshKey, cancellationToken).ConfigureAwait(false);
        var activityRaw = await localStorage.GetItemAsync<string>(LastActivityKey, cancellationToken).ConfigureAwait(false);
        _lastActivityUtc = ParseActivity(activityRaw);
    }

    public async Task TouchActivityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _lastActivityUtc = DateTimeOffset.UtcNow;
        await localStorage.SetItemAsync(LastActivityKey, _lastActivityUtc.Value.ToString("O", CultureInfo.InvariantCulture), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task PersistAsync(string accessToken, string refreshToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _loaded = true;
        await localStorage.SetItemAsync(AccessKey, accessToken, cancellationToken).ConfigureAwait(false);
        await localStorage.SetItemAsync(RefreshKey, refreshToken, cancellationToken).ConfigureAwait(false);
        await TouchActivityAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _accessToken = null;
        _refreshToken = null;
        _lastActivityUtc = null;
        _loaded = false;
        await localStorage.RemoveItemAsync(AccessKey, cancellationToken).ConfigureAwait(false);
        await localStorage.RemoveItemAsync(RefreshKey, cancellationToken).ConfigureAwait(false);
        await localStorage.RemoveItemAsync(LastActivityKey, cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset? ParseActivity(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto
            : null;
    }
}
