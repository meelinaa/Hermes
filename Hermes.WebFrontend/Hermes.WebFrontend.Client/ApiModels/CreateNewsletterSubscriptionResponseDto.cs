namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>Response from <c>POST /api/v1/users/newsletter-subscriptions</c>.</summary>
public sealed class CreateNewsletterSubscriptionResponseDto
{
    public int UserId { get; set; }

    public int SubscriptionId { get; set; }
}
