using System.Net.Http.Json;
using System.Text.Json;
using Hermes.WebFrontend.Client.Services.NewsService;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Hermes.WebFrontend.Client.Services.Auth;

/// <summary>
/// Revokes server sessions, clears stored tokens, drops readable cookies, and hard-navigates to login.
/// </summary>
public sealed class AuthLogoutService(HttpClient http, AuthTokenStore tokens, NewsSubscriptionListCache newsListCache, IJSRuntime js, NavigationManager nav)
{
    private static readonly JsonSerializerOptions JsonWeb = JsonSerializerOptions.Web;

    public async Task SignOutAndReloadAsync(CancellationToken cancellationToken = default)
    {
        await tokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await http
                .PostAsJsonAsync("api/v1/auth/logout", new { }, JsonWeb, cancellationToken)
                .ConfigureAwait(false);
            _ = response;
        }
        catch
        {
            // Still clear client state if API is unreachable.
        }

        await tokens.ClearAsync(cancellationToken).ConfigureAwait(false);
        newsListCache.Invalidate();

        try
        {
            await js.InvokeVoidAsync("hermesAuth.signOutAndReload", "/login");
        }
        catch
        {
            nav.NavigateTo("/login", forceLoad: true);
        }
    }
}
