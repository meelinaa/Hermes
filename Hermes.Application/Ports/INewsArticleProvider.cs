using Hermes.Application.Models.News;

namespace Hermes.Application.Ports;

public interface INewsArticleProvider
{
    Task<IReadOnlyList<NewsArticle>> GetLatestAsync(NewsArticleQuery query, CancellationToken cancellationToken = default);
}
