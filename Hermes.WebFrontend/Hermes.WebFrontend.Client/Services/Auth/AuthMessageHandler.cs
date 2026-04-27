using System.Net.Http.Headers;

namespace Hermes.WebFrontend.Client.Services.Auth;

/// <summary>
/// Adds <c>Authorization: Bearer …</c> when an access token is present (from <see cref="AuthTokenStore"/>).
/// </summary>
public sealed class AuthMessageHandler(AuthTokenStore tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await tokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(tokens.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
