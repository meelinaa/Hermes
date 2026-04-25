using System.Globalization;
using Blazored.LocalStorage;

namespace Hermes.WebFrontend.Client.Services;

/// <summary>
/// Persists JWT access + refresh tokens in browser local storage for API calls.
/// </summary>
public sealed class AuthTokenStore
{
    private const string AccessKey = "hermes.auth.accessToken";
    private const string RefreshKey = "hermes.auth.refreshToken";
    private const string LastActivityKey = "hermes.auth.lastActivityUtc";

    private readonly ILocalStorageService _localStorage;
    private bool _loaded;
    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset? _lastActivityUtc;

    public AuthTokenStore(ILocalStorageService localStorage) => _localStorage = localStorage;

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
        _accessToken = await _localStorage.GetItemAsync<string>(AccessKey).ConfigureAwait(false);
        _refreshToken = await _localStorage.GetItemAsync<string>(RefreshKey).ConfigureAwait(false);
        var activityRaw = await _localStorage.GetItemAsync<string>(LastActivityKey).ConfigureAwait(false);
        _lastActivityUtc = ParseActivity(activityRaw);
    }

    public async Task TouchActivityAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _lastActivityUtc = DateTimeOffset.UtcNow;
        await _localStorage.SetItemAsync(LastActivityKey, _lastActivityUtc.Value.ToString("O", CultureInfo.InvariantCulture))
            .ConfigureAwait(false);
    }

    public async Task PersistAsync(string accessToken, string refreshToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _loaded = true;
        await _localStorage.SetItemAsync(AccessKey, accessToken).ConfigureAwait(false);
        await _localStorage.SetItemAsync(RefreshKey, refreshToken).ConfigureAwait(false);
        await TouchActivityAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _accessToken = null;
        _refreshToken = null;
        _lastActivityUtc = null;
        _loaded = false;
        await _localStorage.RemoveItemAsync(AccessKey).ConfigureAwait(false);
        await _localStorage.RemoveItemAsync(RefreshKey).ConfigureAwait(false);
        await _localStorage.RemoveItemAsync(LastActivityKey).ConfigureAwait(false);
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
