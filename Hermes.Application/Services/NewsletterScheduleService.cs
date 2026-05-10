using Hermes.Application.Mapping;
using Hermes.Application.Ports;
using Hermes.Domain.Enums;

namespace Hermes.Application.Services;

public sealed class NewsletterScheduleService(INewsStore dataStore) : INewsletterScheduleService
{
    /// <summary>Returns all (news,user) pairs due for the one-minute UTC window, using stored next-slot UTC when present.</summary>
    public async Task<IReadOnlyList<(int NewsId, int UserId)>> GetDueItemsAsync(
        DateTime nowLocal,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default)
    {
        TimeOnly nowTime = TimeOnly.FromDateTime(nowLocal);
        Weekdays todayWeekday = WeekdayConverter.ToHermesWeekday(nowLocal);
        return await dataStore
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
