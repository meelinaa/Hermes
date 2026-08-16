using Hermes.WebFrontend.Client.ApiModels;

namespace Hermes.WebFrontend.Client.Services.Api;

/// <summary>
/// API client interface for querying real-time live news feeds and article previews.
/// </summary>
public interface INewsFeedApiClient
{
    /// <summary>
    /// Fetches live news articles matching the supplied search criteria.
    /// </summary>
    /// <param name="request">The search filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>API result containing the list of matching news articles.</returns>
    Task<ApiResult<IReadOnlyList<NewsArticleDto>>> GetPreviewArticlesAsync(NewsPreviewRequestDto request, CancellationToken cancellationToken = default);
}
