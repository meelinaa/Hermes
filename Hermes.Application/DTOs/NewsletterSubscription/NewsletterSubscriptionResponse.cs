using Hermes.Domain.Enums;

namespace Hermes.Application.DTOs.NewsletterSubscription;

/// <summary>
/// Response payload representing the details of a newsletter subscription.
/// </summary>
public sealed record NewsletterSubscriptionResponse
{
    /// <summary>
    /// Gets or sets the unique ID of the newsletter subscription.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets or sets the user ID associated with this subscription.
    /// </summary>
    public int UserId { get; init; }

    /// <summary>
    /// Gets or sets the list of search keywords.
    /// </summary>
    public List<string>? Keywords { get; init; }

    /// <summary>
    /// Gets or sets the categories for this subscription.
    /// </summary>
    public List<NewsCategory>? Category { get; init; }

    /// <summary>
    /// Gets or sets the languages for this subscription.
    /// </summary>
    public List<Language>? Languages { get; init; }

    /// <summary>
    /// Gets or sets the countries for this subscription.
    /// </summary>
    public List<Country>? Countries { get; init; }

    /// <summary>
    /// Gets or sets the weekdays schedule for sending.
    /// </summary>
    public List<Weekdays> SendOnWeekdays { get; init; } = [];

    /// <summary>
    /// Gets or sets the send times for sending.
    /// </summary>
    public List<TimeOnly> SendAtTimes { get; init; } = [];

    /// <summary>
    /// Gets or sets whether the subscription is active.
    /// </summary>
    public bool IsEnabled { get; init; } = true;
}
