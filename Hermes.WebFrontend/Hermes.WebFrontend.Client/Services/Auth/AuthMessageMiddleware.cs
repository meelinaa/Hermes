using System.Net;
using System.Net.Http.Headers;

namespace Hermes.WebFrontend.Client.Services.Auth;

/// <summary>
/// HTTP delegating handler that attaches the JWT Bearer token only to authorized endpoints, proactively ensures the session is fresh, and recovers from 401 Unauthorized by attempting a token refresh.
/// </summary>
public sealed class AuthMessageMiddleware(AuthTokenStore tokens, AuthSessionService session, Uri? authorizedBaseUri = null) : DelegatingHandler
{
    /// <summary>
    /// Attaches the bearer token before sending the HTTP request if destined for an authorized endpoint, and retries the request once after refreshing if a 401 Unauthorized status is returned.
    /// </summary>
    /// <param name="request">The outgoing HTTP request message.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The HTTP response message.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await session.EnsureSessionAsync(cancellationToken).ConfigureAwait(false);

        bool isAuthorized = IsAuthorizedEndpoint(request.RequestUri, authorizedBaseUri);

        if (isAuthorized && !string.IsNullOrEmpty(tokens.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(tokens.RefreshToken) && isAuthorized)
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

    /// <summary>
    /// Evaluates whether the specified request URI belongs to the authorized backend API origin to prevent credential leakage to third parties.
    /// </summary>
    /// <param name="requestUri">The target URI of the request.</param>
    /// <param name="baseUri">The base URI of the authorized backend.</param>
    /// <returns>True if the endpoint is authorized to receive credentials; otherwise, false.</returns>
    public static bool IsAuthorizedEndpoint(Uri? requestUri, Uri? baseUri)
    {
        if (requestUri is null || !requestUri.IsAbsoluteUri)
            return true;

        if (baseUri is null)
            return true;

        return string.Equals(requestUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(requestUri.Authority, baseUri.Authority, StringComparison.OrdinalIgnoreCase);
    }
}
