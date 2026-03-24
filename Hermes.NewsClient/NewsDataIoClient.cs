using System.Net.Http;
using System.Text.Json;
using Hermes.NewsClient.DTOs;

namespace Hermes.NewsClient;

/// <summary>
/// HTTP client for the NewsData.io latest-news API.
/// </summary>
public class NewsDataIoClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of <see cref="NewsDataIoClient"/>.
    /// </summary>
    /// <param name="httpClient">HTTP client used for requests (caller owns its lifetime).</param>
    public NewsDataIoClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Requests the latest news using the parameters in <paramref name="urlParts"/> and deserializes the JSON body.
    /// </summary>
    /// <param name="urlParts">Query parameters; <see cref="ApiUrlParts.ApiKey"/> must be set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized response, or <c>null</c> if the HTTP status indicates failure.</returns>
    public async Task<NewsDataIoDto?> GetLatestAsync(
        ApiUrlParts urlParts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(urlParts);

        var url = NewsDataIoUrlBuilder.Build(urlParts);
        Console.WriteLine(url);
        var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<NewsDataIoDto>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
