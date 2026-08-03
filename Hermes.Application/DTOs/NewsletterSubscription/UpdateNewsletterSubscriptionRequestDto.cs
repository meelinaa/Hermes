using Hermes.Domain.Enums;

namespace Hermes.Application.DTOs.NewsletterSubscription;

/// <summary>
/// Payload containing properties used to update an existing newsletter subscription.
/// </summary>
public sealed record UpdateNewsletterSubscriptionRequestDto
{
    /// <summary>
    /// Gets or sets the unique ID of the newsletter subscription to update.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets or sets the updated list of keywords.
    /// </summary>
    public List<string>? Keywords { get; init; }

    /// <summary>
    /// Gets or sets the updated categories.
    /// </summary>
    public List<NewsCategory>? Category { get; init; }

    /// <summary>
    /// Gets or sets the updated languages filter.
    /// </summary>
    public List<Language>? Languages { get; init; }

    /// <summary>
    /// Gets or sets the updated countries filter.
    /// </summary>
    public List<Country>? Countries { get; init; }

    /// <summary>
    /// Gets or sets the updated weekdays schedule.
    /// </summary>
    public List<Weekdays> SendOnWeekdays { get; init; } = [];

    /// <summary>
    /// Gets or sets the updated time-of-day send triggers.
    /// </summary>
    public List<TimeOnly> SendAtTimes { get; init; } = [];

    /// <summary>
    /// Gets or sets whether the subscription is active.
    /// </summary>
    public bool? IsEnabled { get; init; }
}
