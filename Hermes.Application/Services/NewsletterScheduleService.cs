using Hermes.Application.Mapping;
using Hermes.Application.Ports;
using Hermes.Domain.Enums;

namespace Hermes.Application.Services;

public sealed class NewsletterScheduleService(IHermesDataStore dataStore) : INewsletterScheduleService
{
    /// <summary>Returns all (news,user) pairs that are due for dispatch at the provided local time.</summary>
    /// <remarks>
    /// Matching is delegated to <see cref="IHermesDataStore.GetDueNewsScheduleForSlotAsync"/> so only due rows are read from MySQL.
    /// </remarks>
    public async Task<IReadOnlyList<(int NewsId, int UserId)>> GetDueItemsAsync(DateTime nowLocal, CancellationToken cancellationToken = default)
    {
        TimeOnly nowTime = TimeOnly.FromDateTime(nowLocal);
        Weekdays todayWeekday = WeekdayConverter.ToHermesWeekday(nowLocal);
        return await dataStore
            .GetDueNewsScheduleForSlotAsync(todayWeekday, nowTime.Hour, nowTime.Minute, cancellationToken)
            .ConfigureAwait(false);
    }
}
