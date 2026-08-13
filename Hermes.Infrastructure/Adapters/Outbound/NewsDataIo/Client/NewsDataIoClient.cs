using System.Text.Json;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Ports.Outbound;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Builders;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;

namespace Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Providers;

/// <summary>
/// HTTP client adapter for retrieving latest news articles from the NewsData.io REST API.
/// </summary>
/// <param name="httpClient">The HTTP client instance.</param>
public sealed class NewsDataIoClient(HttpClient httpClient) : INewsArticleProvider
{
    /// <summary>
    /// Fetches latest articles for the supplied query and maps them into application news article models.
    /// </summary>
    /// <param name="query">The news article query criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of fetched news articles.</returns>
    public async Task<IReadOnlyList<NewsArticle>> GetLatestAsync(NewsArticleQueryDto query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApiUrlPartsDto urlParts = new()
        {
            ApiKey = query.ApiKey,
            Countries = query.Countries,
            Languages = query.Languages,
            Categories = query.Categories,
            Q = query.KeywordsQuery,
            Timezone = query.Timezone,
            Image = query.Image,
            RemoveDuplicate = query.RemoveDuplicate,
            Sort = query.Sort,
            ExcludeField = query.ExcludeField
        };

        string url = NewsDataIoUrlUtility.Build(urlParts);
        HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return [];

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        NewsDataIoDto? dto = await JsonSerializer.DeserializeAsync<NewsDataIoDto>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (dto?.Results is null)
            return [];

        return dto.Results.Select(resultItem => new NewsArticle(
            resultItem.ArticleId,
            resultItem.Link,
            resultItem.Title,
            resultItem.Description,
            resultItem.Category,
            resultItem.ImageUrl)).ToList();
    }
}
