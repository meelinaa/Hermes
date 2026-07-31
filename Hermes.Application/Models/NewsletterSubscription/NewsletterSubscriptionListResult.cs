using Hermes.Domain.Entities;

namespace Hermes.Application.Models.NewsletterSubscription;

/// <summary>
/// Execution result of a newsletter subscription list query.
/// </summary>
/// <param name="Items">The list of fetched subscriptions.</param>
/// <param name="Page">The current page.</param>
/// <param name="PageSize">The page size.</param>
/// <param name="TotalCount">The total subscription count.</param>
/// <param name="TotalPages">The total pages.</param>
/// <param name="HasNextPage">Whether a next page exists.</param>
/// <param name="NextAfterId">The keyset cursor for the next page.</param>
public sealed record NewsletterSubscriptionListResult(
    IReadOnlyList<Hermes.Domain.Entities.NewsletterSubscription> Items,
    int Page,
    int PageSize,
    int? TotalCount,
    int? TotalPages,
    bool HasNextPage,
    int? NextAfterId);
