using Hermes.WebFrontend.Client.ApiModels.Enums;

namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>Body for <c>POST /api/v1/users/newsletter-subscriptions</c> (owner from JWT).</summary>
public sealed class CreateNewsletterSubscriptionPayloadDto
{
    public List<string>? Keywords { get; set; }

    public List<NewsCategory>? Category { get; set; }

    public List<Language>? Languages { get; set; }

    public List<Country>? Countries { get; set; }

    public List<Weekdays> SendOnWeekdays { get; set; } = [];

    public List<TimeOnly> SendAtTimes { get; set; } = [];

    public bool IsEnabled { get; set; } = true;
}
