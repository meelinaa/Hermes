using Hermes.Application.Ports;
using Hermes.Domain.Mapping;

namespace Hermes.Application.Services;

public sealed class NewsletterScheduleService(IHermesDataStore dataStore) : INewsletterScheduleService
{
    public async Task<IReadOnlyList<(int NewsId, int UserId)>> GetDueItemsAsync(DateTime nowLocal, CancellationToken cancellationToken = default)
    {
        var nowTime = TimeOnly.FromDateTime(nowLocal);
        var todayWeekday = WeekdayConverter.ToHermesWeekday(nowLocal);
        var rows = await dataStore.GetNewsScheduleRowsAsync(cancellationToken).ConfigureAwait(false);

        var due = new List<(int NewsId, int UserId)>();
        foreach (var row in rows)
        {
            if (row.NewsId <= 0 || row.UserId <= 0)
                continue;
            if (row.SendOnWeekdays is not { Count: > 0 } || !row.SendOnWeekdays.Contains(todayWeekday))
                continue;
            if (row.SendAtTimes is not { Count: > 0 })
                continue;
            if (!row.SendAtTimes.Any(t => t.Hour == nowTime.Hour && t.Minute == nowTime.Minute))
                continue;

            due.Add((row.NewsId, row.UserId));
        }

        return due;
    }
}
