using FluentResults;
using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Inbound;

public interface INewsletterSubscriptionService
{
    ValueTask<Result<NewsletterId>> SetNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    ValueTask<Result> UpdateNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    ValueTask<Result> DeleteNewsAsync(NewsletterSubscription news, CancellationToken cancellationToken = default);

    ValueTask<Result<NewsletterSubscription>> GetNewsByIdAsync(UserId userId, NewsletterId id, CancellationToken cancellationToken = default);

    ValueTask<Result<NewsletterSubscription>> FindNewsByIdAsync(NewsletterId id, CancellationToken cancellationToken = default);

    ValueTask<Result<NewsletterSubscriptionListResultDto>> GetNewsListAsync(NewsletterSubscriptionListQueryDto query, CancellationToken cancellationToken = default);

    ValueTask<Result<int>> DeleteAllNewsByUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
