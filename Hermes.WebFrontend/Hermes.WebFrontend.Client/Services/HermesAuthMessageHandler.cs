using System.Net.Http.Headers;

namespace Hermes.WebFrontend.Client.Services;

/// <summary>
/// Adds <c>Authorization: Bearer …</c> when an access token is present (from <see cref="AuthTokenStore"/>).
/// </summary>
public sealed class HermesAuthMessageHandler : DelegatingHandler
{
    private readonly AuthTokenStore _tokens;

    public HermesAuthMessageHandler(AuthTokenStore tokens) => _tokens = tokens;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _tokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(_tokens.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.AccessToken);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
