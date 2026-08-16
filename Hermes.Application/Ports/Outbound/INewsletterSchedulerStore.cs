using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Outbound port for background scheduler slot evaluation and digest timestamp advancement.
/// </summary>
public interface INewsletterSchedulerStore
{
    /// <summary>
    /// Retrieves newsletter subscription schedules that are due for delivery in the specified slot.
    /// </summary>
    /// <param name="weekday">The target weekday.</param>
    /// <param name="hour">The target hour in local subscriber timezone.</param>
    /// <param name="minute">The target minute in local subscriber timezone.</param>
    /// <param name="slotStartUtc">The UTC slot start timestamp.</param>
    /// <param name="slotEndUtc">The UTC slot end timestamp.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A list of tuples identifying due subscriptions and owner user IDs.</returns>
    ValueTask<List<(NewsletterId NewsId, UserId UserId)>> GetDueNewsScheduleForSlotAsync(
        Weekdays weekday,
        int hour,
        int minute,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Advances the next digest slot timestamp for a newsletter subscription.
    /// </summary>
    /// <param name="newsId">The newsletter subscription ID.</param>
    /// <param name="userId">The owner user ID.</param>
    /// <param name="newsletterTimeZone">The subscription timezone.</param>
    /// <param name="referenceUtcExclusive">The reference timestamp for slot calculation.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask AdvanceNextDigestSlotAsync(
        NewsletterId newsId,
        UserId userId,
        TimeZoneInfo newsletterTimeZone,
        DateTime referenceUtcExclusive,
        CancellationToken cancellationToken = default);
}
