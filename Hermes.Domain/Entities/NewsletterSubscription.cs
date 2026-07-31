using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;

/// <summary>
/// Represents a user's subscription configuration for news digests (newsletters),
/// specifying keywords, categories, languages, countries, and schedule.
/// </summary>
public class NewsletterSubscription
{
    /// <summary>
    /// Gets or sets the unique identifier of the newsletter subscription.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who owns this subscription.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Gets or sets the list of keywords to search for articles.
    /// </summary>
    public List<string>? Keywords { get; set; }

    /// <summary>
    /// Gets or sets the list of news categories included in the newsletter.
    /// </summary>
    public List<NewsCategory>? Category { get; set; }

    /// <summary>
    /// Gets or sets the list of languages for the newsletter articles.
    /// </summary>
    public List<Language>? Languages { get; set; }

    /// <summary>
    /// Gets or sets the list of countries for the newsletter articles.
    /// </summary>
    public List<Country>? Countries { get; set; }

    /// <summary>
    /// Gets or sets the list of weekdays when the newsletter should be sent.
    /// </summary>
    public List<Weekdays> SendOnWeekdays { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of times in a day when the newsletter should be sent.
    /// </summary>
    public List<TimeOnly> SendAtTimes { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether this newsletter subscription is currently enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Materialized next digest eligibility (UTC minute boundary); query path may use JSON when unset.
    /// </summary>
    public DateTime? NextDigestSlotUtc { get; set; }

    /// <summary>
    /// Assigns the schedule window configuration (weekdays and times) to this newsletter subscription.
    /// </summary>
    /// <param name="schedule">The schedule window configuration to apply.</param>
    public void AssignDigestSchedule(ScheduleWindow schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        schedule.ApplyToSubscription(this);
    }
}
