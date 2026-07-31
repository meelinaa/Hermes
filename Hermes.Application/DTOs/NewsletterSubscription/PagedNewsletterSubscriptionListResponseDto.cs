namespace Hermes.Application.DTOs.NewsletterSubscription;

/// <summary>
/// Paged response body containing a list of newsletter subscription responses.
/// </summary>
/// <param name="Items">The list of subscription responses.</param>
/// <param name="Page">The current page.</param>
/// <param name="PageSize">The page size.</param>
/// <param name="TotalCount">The total count.</param>
/// <param name="TotalPages">The total pages.</param>
/// <param name="HasNextPage">Whether a next page is available.</param>
/// <param name="NextAfterId">The keyset cursor for the next page.</param>
public sealed record PagedNewsletterSubscriptionListResponseDto(
    IReadOnlyList<NewsletterSubscriptionResponseDto> Items,
    int Page,
    int PageSize,
    int? TotalCount,
    int? TotalPages,
    bool HasNextPage,
    int? NextAfterId);
