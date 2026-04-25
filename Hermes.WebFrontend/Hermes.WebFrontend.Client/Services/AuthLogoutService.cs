using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Hermes.WebFrontend.Client.Services;

/// <summary>
/// Revokes server sessions, clears stored tokens, drops readable cookies, and hard-navigates to login.
/// </summary>
public sealed class AuthLogoutService
{
    private static readonly JsonSerializerOptions JsonWeb = JsonSerializerOptions.Web;

    private readonly HttpClient _http;
    private readonly AuthTokenStore _tokens;
    private readonly NewsSubscriptionListCache _newsListCache;
    private readonly IJSRuntime _js;
    private readonly NavigationManager _nav;

    public AuthLogoutService(
        HttpClient http,
        AuthTokenStore tokens,
        NewsSubscriptionListCache newsListCache,
        IJSRuntime js,
        NavigationManager nav)
    {
        _http = http;
        _tokens = tokens;
        _newsListCache = newsListCache;
        _js = js;
        _nav = nav;
    }

    public async Task SignOutAndReloadAsync(CancellationToken cancellationToken = default)
    {
        await _tokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await _http
                .PostAsJsonAsync("api/v1/auth/logout", new { }, JsonWeb, cancellationToken)
                .ConfigureAwait(false);
            _ = response;
        }
        catch
        {
            // Still clear client state if API is unreachable.
        }

        await _tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
        _newsListCache.Invalidate();

        try
        {
            await _js.InvokeVoidAsync("hermesAuth.signOutAndReload", "/login");
        }
        catch
        {
            _nav.NavigateTo("/login", forceLoad: true);
        }
    }
}
