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

    /// <summary>
    /// Private parameterless constructor required by EF Core for entity materialization.
    /// External callers must use <see cref="CreateForUser"/> to ensure valid aggregate state.
    /// </summary>
    private NewsletterSubscription() { }

    /// <summary>
    /// Factory method that creates a new newsletter subscription instance bound to the specified owner user.
    /// Enforces that the user ID invariant is strictly positive.
    /// </summary>
    /// <param name="userId">The strongly-typed identifier of the owning user.</param>
    /// <returns>A new <see cref="NewsletterSubscription"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="userId"/> is zero or negative.</exception>
    public static NewsletterSubscription CreateForUser(UserId userId)
    {
        if (userId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");
        return new NewsletterSubscription { UserId = userId };
    }

    /// <summary>
    /// Updates the topic, category, language, and country filtering criteria for news article matching.
    /// Sanitizes keyword inputs by trimming whitespace and removing empty entries.
    /// </summary>
    /// <param name="keywords">The search keywords or topics.</param>
    /// <param name="categories">The news categories to include.</param>
    /// <param name="languages">The languages to filter articles by.</param>
    /// <param name="countries">The countries to filter articles by.</param>
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

    /// <summary>
    /// Enables this newsletter subscription so it is considered active during recurring background scheduling.
    /// </summary>
    public void Enable() => IsEnabled = true;

    /// <summary>
    /// Disables this newsletter subscription to pause recurring background scheduling without deleting configuration.
    /// </summary>
    public void Disable() => IsEnabled = false;

    /// <summary>
    /// Sets the database-generated surrogate identifier. Internal to infrastructure persistence adapters.
    /// </summary>
    /// <param name="id">The strongly-typed newsletter subscription ID.</param>
    internal void SetId(NewsletterId id) => Id = id;

    /// <summary>
    /// Sets the owning user ID. Internal to infrastructure persistence adapters.
    /// </summary>
    /// <param name="userId">The strongly-typed user ID.</param>
    internal void SetUserId(UserId userId) => UserId = userId;

    /// <summary>
    /// Assigns the schedule window configuration (weekdays and times) to this newsletter subscription.
    /// Enforces the schedule invariant via <see cref="ScheduleWindow"/>.
    /// </summary>
    /// <param name="schedule">The schedule window configuration to apply.</param>
    public void AssignDigestSchedule(ScheduleWindow schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        schedule.ApplyToSubscription(this);
    }

    /// <summary>
    /// Sets the weekdays and times on which the digest should be dispatched.
    /// Invoked internally by <see cref="ScheduleWindow.ApplyToSubscription"/> to guarantee valid schedule combinations.
    /// </summary>
    /// <param name="weekdays">The collection of active weekdays.</param>
    /// <param name="times">The collection of active daily send times.</param>
    internal void SetSchedule(IReadOnlyList<Weekdays> weekdays, IReadOnlyList<TimeOnly> times)
    {
        SendOnWeekdays = weekdays;
        SendAtTimes = times;
    }

    /// <summary>
    /// Sets the calculated next eligibility time slot in UTC for fast database index-backed querying.
    /// </summary>
    /// <param name="next">The next UTC dispatch time slot, or null if no further runs are scheduled.</param>
    public void SetNextDigestSlot(DateTime? next) => NextDigestSlotUtc = next;
}
