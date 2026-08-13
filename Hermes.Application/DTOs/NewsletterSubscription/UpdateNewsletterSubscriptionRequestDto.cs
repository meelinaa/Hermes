using Hermes.Domain.Enums;

namespace Hermes.Application.DTOs.NewsletterSubscription;

/// <summary>
/// Data transfer object used to overwrite an existing newsletter configuration.
/// Ensures partial updates are applied correctly and forces a recalculation of the next delivery window.
/// </summary>
public sealed record UpdateNewsletterSubscriptionRequestDto
{
    /// <summary>
    /// The unique identifier of the target subscription to modify. Ensures updates affect the correct entity.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Free-text search terms passed directly to the external news provider API to filter matching articles.
    /// </summary>
    public List<string>? Keywords { get; init; }

    /// <summary>
    /// Broad topical categories (e.g., Technology, Business) to restrict the scope of the generated digest.
    /// </summary>
    public List<NewsCategory>? Category { get; init; }

    /// <summary>
    /// ISO-639-1 language codes ensuring the user only receives content in languages they understand.
    /// </summary>
    public List<Language>? Languages { get; init; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country codes to localize the news digest to specific geographic regions.
    /// </summary>
    public List<Country>? Countries { get; init; }

    /// <summary>
    /// Determines on which days of the week the background job should generate and dispatch the digest.
    /// </summary>
    public List<Weekdays> SendOnWeekdays { get; init; } = [];

    /// <summary>
    /// Exact times (in the user's local timezone) when the delivery engine should trigger the email generation.
    /// </summary>
    public List<TimeOnly> SendAtTimes { get; init; } = [];

    /// <summary>
    /// Acts as a soft-kill switch. If false, the subscription is ignored by the delivery scheduler without losing its configuration.
    /// </summary>
    public bool? IsEnabled { get; init; }
}
