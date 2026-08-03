namespace Hermes.Application.DTOs.NewsletterSubscription;

/// <summary>
/// Response payload summarizing a bulk deletion of newsletter subscriptions.
/// </summary>
/// <param name="Deleted">The total count of subscriptions that were deleted.</param>
public sealed record DeleteAllNewsletterSubscriptionResponseDto(int Deleted);
