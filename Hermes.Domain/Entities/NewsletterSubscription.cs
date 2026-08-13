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
    public NewsletterId Id { get; private set; }

    /// <summary>
    /// Gets or sets the ID of the user who owns this subscription.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets or sets the list of keywords to search for articles.
    /// </summary>
    public IReadOnlyList<string>? Keywords { get; private set; }

    /// <summary>
    /// Gets or sets the list of news categories included in the newsletter.
    /// </summary>
    public IReadOnlyList<NewsCategory>? Category { get; private set; }

    /// <summary>
    /// Gets or sets the list of languages for the newsletter articles.
    /// </summary>
    public IReadOnlyList<Language>? Languages { get; private set; }

    /// <summary>
    /// Gets or sets the list of countries for the newsletter articles.
    /// </summary>
    public IReadOnlyList<Country>? Countries { get; private set; }

    /// <summary>
    /// Gets or sets the list of weekdays when the newsletter should be sent.
    /// </summary>
    public IReadOnlyList<Weekdays> SendOnWeekdays { get; private set; } = [];

    /// <summary>
    /// Gets or sets the list of times in a day when the newsletter should be sent.
    /// </summary>
    public IReadOnlyList<TimeOnly> SendAtTimes { get; private set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether this newsletter subscription is currently enabled.
    /// </summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Materialized next digest eligibility (UTC minute boundary); query path may use JSON when unset.
    /// </summary>
    public DateTime? NextDigestSlotUtc { get; private set; }

    public static NewsletterSubscription CreateForUser(UserId userId)
    {
        if (userId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");
        return new NewsletterSubscription { UserId = userId };
    }

    public void UpdateFilters(
        IEnumerable<string>? keywords,
        IEnumerable<NewsCategory>? categories,
        IEnumerable<Language>? languages,
        IEnumerable<Country>? countries)
    {
        Keywords = keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList();
        Category = categories?.ToList();
        Languages = languages?.ToList();
        Countries = countries?.ToList();
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;

    internal void SetId(NewsletterId id) => Id = id;

    internal void SetUserId(UserId userId) => UserId = userId;

    /// <summary>
    /// Assigns the schedule window configuration (weekdays and times) to this newsletter subscription.
    /// </summary>
    /// <param name="schedule">The schedule window configuration to apply.</param>
    public void AssignDigestSchedule(ScheduleWindow schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        schedule.ApplyToSubscription(this);
    }

    internal void SetSchedule(IReadOnlyList<Weekdays> weekdays, IReadOnlyList<TimeOnly> times)
    {
        SendOnWeekdays = weekdays;
        SendAtTimes = times;
    }

    public void SetNextDigestSlot(DateTime? next) => NextDigestSlotUtc = next;
}
