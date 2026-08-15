using System.Net;
using System.Net.Http.Headers;

namespace Hermes.WebFrontend.Client.Services.Auth;

/// <summary>
/// HTTP delegating handler that attaches the JWT Bearer token, proactively ensures the session is fresh, and recovers from 401 Unauthorized by attempting a token refresh.
/// </summary>
public sealed class AuthMessageMiddleware(AuthTokenStore tokens, AuthSessionService session) : DelegatingHandler
{
    /// <summary>
    /// Attaches the bearer token before sending the HTTP request, and retries the request once after refreshing if a 401 Unauthorized status is returned.
    /// </summary>
    /// <param name="request">The outgoing HTTP request message.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The HTTP response message.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await session.EnsureSessionAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(tokens.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(tokens.RefreshToken))
        {
            bool refreshed = await session.EnsureSessionAsync(cancellationToken).ConfigureAwait(false);
            if (refreshed && !string.IsNullOrEmpty(tokens.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
                response.Dispose();
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        return response;
    }
}
