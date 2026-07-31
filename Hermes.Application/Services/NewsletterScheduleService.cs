using Hermes.Application.Mapping;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Enums;

namespace Hermes.Application.Services;

/// <summary>
/// Service implementation for determining which newsletter schedules are due for processing.
/// </summary>
public sealed class NewsletterScheduleService(INewsletterSubscriptionRepository newsletterSubscriptionRepository) : INewsletterScheduleService
{
    /// <summary>
    /// Evaluates current local time and UTC windows to identify due newsletter subscriptions.
    /// </summary>
    public async Task<IReadOnlyList<(int NewsId, int UserId)>> GetDueItemsAsync(
        DateTime nowLocal,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default)
    {
        TimeOnly nowTime = TimeOnly.FromDateTime(nowLocal);
        Weekdays todayWeekday = WeekdayMapper.ToHermesWeekday(nowLocal);
        return await newsletterSubscriptionRepository
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
