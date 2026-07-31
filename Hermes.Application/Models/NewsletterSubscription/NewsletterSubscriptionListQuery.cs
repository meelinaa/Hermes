using Hermes.Domain.Enums;

namespace Hermes.Application.Models.NewsletterSubscription;

/// <summary>
/// Query parameters used for fetching a paged list of newsletter subscriptions.
/// </summary>
/// <param name="UserId">The ID of the user owning the subscriptions.</param>
/// <param name="Page">The page number.</param>
/// <param name="PageSize">The page size.</param>
/// <param name="AfterId">The cursor identifier for keyset paging.</param>
/// <param name="SortDescending">Whether sorting should be descending.</param>
/// <param name="Search">An optional search term.</param>
/// <param name="Category">An optional category filter.</param>
public sealed record NewsletterSubscriptionListQuery(
    int UserId,
    int Page,
    int PageSize,
    int? AfterId,
    bool SortDescending,
    string? Search,
    NewsCategory? Category);
