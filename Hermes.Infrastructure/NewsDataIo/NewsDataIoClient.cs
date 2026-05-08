using System.Text.Json;
using Hermes.Application.Models.News;
using Hermes.Application.Ports;

namespace Hermes.Infrastructure.NewsDataIo;

public sealed class NewsDataIoClient(HttpClient httpClient) : INewsArticleProvider
{
    public async Task<IReadOnlyList<NewsArticle>> GetLatestAsync(NewsArticleQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApiUrlParts urlParts = new()
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

        string url = NewsDataIoUrlBuilder.Build(urlParts);
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
