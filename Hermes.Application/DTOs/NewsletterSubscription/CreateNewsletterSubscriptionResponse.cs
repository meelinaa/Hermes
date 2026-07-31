namespace Hermes.Application.DTOs.NewsletterSubscription;

/// <summary>
/// Response payload returned after a newsletter subscription is successfully created.
/// </summary>
/// <param name="UserId">The ID of the user owning the subscription.</param>
/// <param name="SubscriptionId">The ID of the created newsletter subscription.</param>
public sealed record CreateNewsletterSubscriptionResponse(int UserId, int SubscriptionId);
