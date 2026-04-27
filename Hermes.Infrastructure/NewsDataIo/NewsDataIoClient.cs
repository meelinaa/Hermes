using System.Text.Json;
using Hermes.Application.Models.News;
using Hermes.Application.Ports;

namespace Hermes.Infrastructure.NewsDataIo;

public sealed class NewsDataIoClient(HttpClient httpClient) : INewsArticleProvider
{
    public async Task<IReadOnlyList<NewsArticle>> GetLatestAsync(NewsArticleQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var urlParts = new ApiUrlParts
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

        var url = NewsDataIoUrlBuilder.Build(urlParts);
        var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return [];

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var dto = await JsonSerializer.DeserializeAsync<NewsDataIoDto>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (dto?.Results is null)
            return [];

        return dto.Results.Select(r => new NewsArticle(
            r.ArticleId,
            r.Link,
            r.Title,
            r.Description,
            r.Category,
            r.ImageUrl)).ToList();
    }
}
