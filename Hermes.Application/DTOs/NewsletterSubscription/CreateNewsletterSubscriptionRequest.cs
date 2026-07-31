using Hermes.Domain.Enums;

namespace Hermes.Application.DTOs.NewsletterSubscription;

/// <summary>
/// Payload containing properties required to create a new newsletter subscription.
/// </summary>
public sealed record CreateNewsletterSubscriptionRequest
{
    /// <summary>
    /// Gets or sets the list of search keywords.
    /// </summary>
    public List<string>? Keywords { get; init; }

    /// <summary>
    /// Gets or sets the news categories.
    /// </summary>
    public List<NewsCategory>? Category { get; init; }

    /// <summary>
    /// Gets or sets the languages filter.
    /// </summary>
    public List<Language>? Languages { get; init; }

    /// <summary>
    /// Gets or sets the countries filter.
    /// </summary>
    public List<Country>? Countries { get; init; }

    /// <summary>
    /// Gets or sets the weekdays schedule.
    /// </summary>
    public List<Weekdays> SendOnWeekdays { get; init; } = [];

    /// <summary>
    /// Gets or sets the time-of-day send triggers.
    /// </summary>
    public List<TimeOnly> SendAtTimes { get; init; } = [];

    /// <summary>
    /// Gets or sets whether the subscription is active.
    /// </summary>
    public bool? IsEnabled { get; init; }
}
