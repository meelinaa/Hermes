using Hermes.Application.DTOs.NewsArticle;

namespace Hermes.Application.Ports;

public interface INewsArticleProvider
{
    Task<IReadOnlyList<NewsArticle>> GetLatestAsync(NewsArticleQuery query, CancellationToken cancellationToken = default);
}
