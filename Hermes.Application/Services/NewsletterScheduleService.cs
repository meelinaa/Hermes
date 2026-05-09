using Hermes.Application.Mapping;
using Hermes.Application.Ports;
using Hermes.Domain.DTOs;
using Hermes.Domain.Enums;

namespace Hermes.Application.Services;

public sealed class NewsletterScheduleService(IHermesDataStore dataStore) : INewsletterScheduleService
{
    /// <summary>Returns all (news,user) pairs that are due for dispatch at the provided local time.</summary>
    public async Task<IReadOnlyList<(int NewsId, int UserId)>> GetDueItemsAsync(DateTime nowLocal, CancellationToken cancellationToken = default)
    {
        TimeOnly nowTime = TimeOnly.FromDateTime(nowLocal);
        Weekdays todayWeekday = WeekdayConverter.ToHermesWeekday(nowLocal);
        List<NewsScheduleRow> rows = await dataStore.GetNewsScheduleRowsAsync(cancellationToken).ConfigureAwait(false);

        List<(int NewsId, int UserId)> due = [];
        foreach (NewsScheduleRow row in rows)
        {
            if (row.NewsId <= 0 || row.UserId <= 0)
                continue;
            if (row.SendOnWeekdays is not { Count: > 0 } || !row.SendOnWeekdays.Contains(todayWeekday))
                continue;
            if (row.SendAtTimes is not { Count: > 0 })
                continue;
            if (!row.SendAtTimes.Any(scheduledTime => scheduledTime.Hour == nowTime.Hour && scheduledTime.Minute == nowTime.Minute))
                continue;

            due.Add((row.NewsId, row.UserId));
        }

        return due;
    }
}
