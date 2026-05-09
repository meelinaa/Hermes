using Hermes.Application.Mapping;
using Hermes.Application.Ports;
using Hermes.Domain.Enums;

namespace Hermes.Application.Services;

public sealed class NewsletterScheduleService(IHermesDataStore dataStore) : INewsletterScheduleService
{
    /// <summary>Returns all (news,user) pairs that are due for dispatch at the provided wall-clock time.</summary>
    /// <remarks>
    /// <paramref name="nowLocal"/> must be the calendar date and clock time in the same zone users use when
    /// configuring <c>SendOnWeekdays</c> / <c>SendAtTimes</c> (the worker passes “now” from
    /// <see cref="NewsletterSchedulingClock"/> / configured <c>Newsletter:TimeZoneId</c>). Matching is delegated to
    /// <see cref="IHermesDataStore.GetDueNewsScheduleForSlotAsync"/>.
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
