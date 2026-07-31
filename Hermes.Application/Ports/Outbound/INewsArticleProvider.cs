using Hermes.Application.DTOs.NewsArticle;

namespace Hermes.Application.Ports.Outbound;

using Hermes.Application.Ports;

public interface INewsArticleProvider
{
    Task<IReadOnlyList<NewsArticle>> GetLatestAsync(NewsArticleQueryDto query, CancellationToken cancellationToken = default);
}
