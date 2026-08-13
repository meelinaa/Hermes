using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Domain.Entities;

namespace Hermes.Application.Services.Newsletter;

public interface IArticleFetchingService
{
    Task<IReadOnlyList<NewsArticle>> FetchArticlesForSubscriptionAsync(NewsletterSubscription subscription, CancellationToken cancellationToken = default);
}
