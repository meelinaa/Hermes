using Hermes.Application.DTOs.NewsArticle;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Outbound port for fetching the latest news articles from an external provider.
/// </summary>
public interface INewsArticleProvider
{
    /// <summary>
    /// Retrieves the latest news articles matching the given query criteria.
    /// </summary>
    Task<IReadOnlyList<NewsArticle>> GetLatestAsync(NewsArticleQueryDto query, CancellationToken cancellationToken = default);
}
