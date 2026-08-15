using System.Text.Json;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Ports.Outbound;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Builders;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;

namespace Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Providers;

/// <summary>
/// HTTP client adapter for retrieving latest news articles from external news APIs (NewsAPI.org &amp; NewsData.io).
/// </summary>
/// <param name="httpClient">The HTTP client instance.</param>
public sealed class NewsDataIoClient(HttpClient httpClient) : INewsArticleProvider
{
    /// <summary>
    /// Fetches latest articles for the supplied query and maps them into application news article models with full deep-link URLs.
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

        using HttpRequestMessage requestMessage = new(HttpMethod.Get, url);
        requestMessage.Headers.Add("User-Agent", "Hermes-News-App/1.0");
        if (!string.IsNullOrWhiteSpace(query.ApiKey))
        {
            requestMessage.Headers.Add("X-Api-Key", query.ApiKey);
        }

        HttpResponseMessage response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return [];

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        NewsDataIoDto? dto = await JsonSerializer.DeserializeAsync<NewsDataIoDto>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (dto is null)
            return [];

        List<NewsArticle> articles = [];
        foreach (ResultsDto resultItem in dto.AllArticles)
        {
            string? link = resultItem.ResolvedLink;
            if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(resultItem.Title))
                continue;

            List<string>? categories = resultItem.Category;
            if ((categories is null || categories.Count == 0) && resultItem.Source?.Name != null)
            {
                categories = [resultItem.Source.Name];
            }

            articles.Add(new NewsArticle(
                resultItem.ArticleId ?? link,
                link,
                resultItem.Title,
                resultItem.Description,
                categories,
                resultItem.ResolvedImageUrl));
        }

        return articles;
    }
}
