using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Domain.Entities;

namespace Hermes.Application.Services.Newsletter;

/// <summary>
/// Service interface for fetching articles from external news providers for subscriptions and previews.
/// </summary>
public interface IArticleFetchingService
{
    /// <summary>
    /// Fetches news articles for the given newsletter subscription.
    /// </summary>
    Task<IReadOnlyList<NewsArticle>> FetchArticlesForSubscriptionAsync(NewsletterSubscription subscription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches news articles for real-time live preview matching arbitrary query criteria.
    /// </summary>
    Task<IReadOnlyList<NewsArticle>> FetchPreviewArticlesAsync(NewsPreviewRequestDto request, CancellationToken cancellationToken = default);
}
