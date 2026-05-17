using Hermes.Domain.Entities;
using Hermes.Domain.Enums;

namespace Hermes.Domain.ValueObjects;

/// <summary>Digest schedule invariant: at least one weekday and one send time.</summary>
public sealed record ScheduleWindow
{
    public IReadOnlyList<Weekdays> Weekdays { get; }
    public IReadOnlyList<TimeOnly> Times { get; }

    private ScheduleWindow(IReadOnlyList<Weekdays> weekdays, IReadOnlyList<TimeOnly> times)
    {
        Weekdays = weekdays;
        Times = times;
    }

    public static ScheduleWindow EnsureForDigestScheduling(IEnumerable<Weekdays>? weekdays, IEnumerable<TimeOnly>? times)
    {
        List<Weekdays> wd = weekdays is null ? [] : weekdays.Distinct().OrderBy(d => (int)d).ToList();
        List<TimeOnly> tm = times is null ? [] : times.Distinct().OrderBy(t => t).ToList();

        if (wd.Count == 0 || tm.Count == 0)
        {
            throw new ArgumentException(
                "A news digest subscription requires at least one weekday and at least one send time.");
        }

        return new ScheduleWindow(wd, tm);
    }

    public void ApplyToNews(News news)
    {
        ArgumentNullException.ThrowIfNull(news);
        news.SendOnWeekdays = Weekdays.ToList();
        news.SendAtTimes = Times.ToList();
    }
}
