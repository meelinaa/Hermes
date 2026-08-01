using Hermes.Application.DTOs.NewsArticle;

using Hermes.Application.Ports;

namespace Hermes.Application.Ports.Outbound;

public interface INewsArticleProvider
{
    Task<IReadOnlyList<NewsArticle>> GetLatestAsync(NewsArticleQueryDto query, CancellationToken cancellationToken = default);
}
