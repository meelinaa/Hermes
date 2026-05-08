using Hermes.WebFrontend.Client.Model;
using Hermes.WebFrontend.Client.Services.User;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace Hermes.WebFrontend.Client.Services.Auth;

/// <summary>
/// Validates sliding idle timeout, JWT access <c>exp</c>, and refreshes tokens via the API.
/// </summary>
public sealed class AuthSessionService(AuthTokenStore tokens, IHttpClientFactory httpFactory, IConfiguration config)
{
    public const string ANONYMOUS_HTTP_CLIENT_NAME = "HermesApiAnonymous";

    private static readonly JsonSerializerOptions _jsonWeb = JsonSerializerOptions.Web;
    private static readonly TimeSpan _expirationClockSkew = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private sealed record RefreshTokenRequestBody(string RefreshToken);

    /// <summary>
    /// Ensures tokens reflect a valid session: applies idle timeout, renews expired access via refresh, updates activity when authenticated.
    /// </summary>
    /// <returns><c>true</c> if the user has a usable access token afterward.</returns>
    public async Task<bool> EnsureSessionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await tokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);

            bool hasAccess = !string.IsNullOrEmpty(tokens.AccessToken);
            bool hasRefresh = !string.IsNullOrEmpty(tokens.RefreshToken);
            if (!hasAccess && !hasRefresh)
                return false;

            int idleDays = GetIdleTimeoutDays();
            DateTimeOffset? last = tokens.LastActivityUtc;
            if (last.HasValue && DateTimeOffset.UtcNow - last.Value > TimeSpan.FromDays(idleDays))
            {
                await tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            if (hasAccess && IsAccessTokenAlive(tokens.AccessToken))
            {
                await tokens.TouchActivityAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (!hasRefresh)
            {
                await tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            bool refreshed = await TryRefreshAsync(cancellationToken).ConfigureAwait(false);
            if (!refreshed)
            {
                await tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
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

    private static bool IsAccessTokenAlive(string? accessToken)
    {
        DateTimeOffset? exp = JwtPayloadDisplayName.TryGetExpiresAtUtc(accessToken);
        if (!exp.HasValue)
            return false;
        return exp.Value > DateTimeOffset.UtcNow.Add(_expirationClockSkew);
    }

    private int GetIdleTimeoutDays()
    {
        string? configuredIdleDays = config["Session:IdleTimeoutDays"];
        if (int.TryParse(configuredIdleDays, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedIdleDays) && parsedIdleDays > 0)
            return parsedIdleDays;
        return 7;
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        string? refresh = tokens.RefreshToken;
        if (string.IsNullOrWhiteSpace(refresh))
            return false;

        try
        {
            HttpClient client = httpFactory.CreateClient(ANONYMOUS_HTTP_CLIENT_NAME);
            using HttpResponseMessage response = await client
                .PostAsJsonAsync("api/v1/auth/refresh", new RefreshTokenRequestBody(refresh.Trim()), _jsonWeb, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return false;

            AuthLoginResponse? body = await response.Content.ReadFromJsonAsync<AuthLoginResponse>(_jsonWeb, cancellationToken).ConfigureAwait(false);
            if (body is null || string.IsNullOrEmpty(body.AccessToken) || string.IsNullOrEmpty(body.RefreshToken))
                return false;

            await tokens.PersistAsync(body.AccessToken, body.RefreshToken, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
