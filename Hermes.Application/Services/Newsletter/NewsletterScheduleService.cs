using Hermes.Application.Mapping;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services.Newsletter;

/// <summary>
/// Evaluates newsletter dispatch schedules against system time slots to identify pending digest tasks for background worker polling.
/// Follows ISP by depending strictly on <see cref="INewsletterSchedulerStore"/>.
/// </summary>
public sealed class NewsletterScheduleService(INewsletterSchedulerStore schedulerStore) : INewsletterScheduleService
{
    /// <summary>
    /// Translates local wall-clock time into Hermes weekday enums and queries repository stores for due newsletter subscriptions.
    /// </summary>
    /// <param name="nowLocal">The current local date and time of the schedule evaluator.</param>
    /// <param name="slotStartUtc">The UTC timestamp marking the beginning of the evaluation time slot.</param>
    /// <param name="slotEndUtc">The UTC timestamp marking the end of the evaluation time slot.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during async database lookup.</param>
    /// <returns>A collection of tuple pairs containing the due subscription ID (<c>NewsId</c>) and owning user ID (<c>UserId</c>).</returns>
    public async Task<IReadOnlyList<(NewsletterId NewsId, UserId UserId)>> GetDueItemsAsync(
        DateTime nowLocal,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default)
    {
        TimeOnly nowTime = TimeOnly.FromDateTime(nowLocal);
        Weekdays todayWeekday = WeekdayMapper.ToHermesWeekday(nowLocal);
        return await schedulerStore
            .GetDueNewsScheduleForSlotAsync(
                todayWeekday,
                nowTime.Hour,
                nowTime.Minute,
                slotStartUtc,
                slotEndUtc,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
