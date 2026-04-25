using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hermes.WebFrontend.Client.Services;

/// <summary>
/// Validates sliding idle timeout, JWT access <c>exp</c>, and refreshes tokens via the API.
/// </summary>
public sealed class AuthSessionService
{
    public const string AnonymousHttpClientName = "HermesApiAnonymous";

    private static readonly JsonSerializerOptions JsonWeb = JsonSerializerOptions.Web;
    private static readonly TimeSpan ExpirationClockSkew = TimeSpan.FromMinutes(2);

    private readonly AuthTokenStore _tokens;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AuthSessionService(
        AuthTokenStore tokens,
        IHttpClientFactory httpFactory,
        IConfiguration config)
    {
        _tokens = tokens;
        _httpFactory = httpFactory;
        _config = config;
    }

    /// <summary>
    /// Ensures tokens reflect a valid session: applies idle timeout, renews expired access via refresh, updates activity when authenticated.
    /// </summary>
    /// <returns><c>true</c> if the user has a usable access token afterward.</returns>
    public async Task<bool> EnsureSessionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _tokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);

            var hasAccess = !string.IsNullOrEmpty(_tokens.AccessToken);
            var hasRefresh = !string.IsNullOrEmpty(_tokens.RefreshToken);
            if (!hasAccess && !hasRefresh)
                return false;

            var idleDays = GetIdleTimeoutDays();
            var last = _tokens.LastActivityUtc;
            if (last.HasValue && DateTimeOffset.UtcNow - last.Value > TimeSpan.FromDays(idleDays))
            {
                await _tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            if (hasAccess && IsAccessTokenAlive(_tokens.AccessToken))
            {
                await _tokens.TouchActivityAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (!hasRefresh)
            {
                await _tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            var refreshed = await TryRefreshAsync(cancellationToken).ConfigureAwait(false);
            if (!refreshed)
            {
                await _tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            // PersistAsync already updates last activity.
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsAccessTokenAlive(string? accessToken)
    {
        var exp = JwtPayloadDisplayName.TryGetExpiresAtUtc(accessToken);
        if (!exp.HasValue)
            return false;
        return exp.Value > DateTimeOffset.UtcNow.Add(ExpirationClockSkew);
    }

    private int GetIdleTimeoutDays()
    {
        var s = _config["Session:IdleTimeoutDays"];
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) && d > 0)
            return d;
        return 7;
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        var refresh = _tokens.RefreshToken;
        if (string.IsNullOrWhiteSpace(refresh))
            return false;

        try
        {
            var client = _httpFactory.CreateClient(AnonymousHttpClientName);
            using var response = await client
                .PostAsJsonAsync("api/v1/auth/refresh", new RefreshTokenRequestBody(refresh.Trim()), JsonWeb, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;

            var body = await response.Content.ReadFromJsonAsync<AuthLoginResponse>(JsonWeb, cancellationToken).ConfigureAwait(false);
            if (body is null || string.IsNullOrEmpty(body.AccessToken) || string.IsNullOrEmpty(body.RefreshToken))
                return false;

            await _tokens.PersistAsync(body.AccessToken, body.RefreshToken, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record RefreshTokenRequestBody(string RefreshToken);
}
