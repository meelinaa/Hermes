using System.Net.Http.Json;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.Services.NewsService;

namespace Hermes.WebFrontend.Client.Services.Api;

/// <summary>
/// Client implementation for fetching live news articles from the API.
/// </summary>
public sealed class NewsFeedApiClient(HttpClient httpClient) : INewsFeedApiClient
{
    /// <inheritdoc />
    public async Task<ApiResult<IReadOnlyList<NewsArticleDto>>> GetPreviewArticlesAsync(NewsPreviewRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/v1/news/preview", request, HermesNewsJsonMapper.Options, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var (errorMessage, problemType, validationErrors) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                return ApiResult<IReadOnlyList<NewsArticleDto>>.Failure(errorMessage, problemType, (int)response.StatusCode, validationErrors);
            }

            IReadOnlyList<NewsArticleDto>? articles = await response.Content.ReadFromJsonAsync<IReadOnlyList<NewsArticleDto>>(HermesNewsJsonMapper.Options, cancellationToken: cancellationToken).ConfigureAwait(false);
            return ApiResult<IReadOnlyList<NewsArticleDto>>.Success(articles ?? []);
        }
        catch (Exception ex)
        {
            return ApiResult<IReadOnlyList<NewsArticleDto>>.Failure($"Verbindungsfehler beim Laden der News: {ex.Message}");
        }
    }
}
