using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;

public class News
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public List<string>? Keywords { get; set; }

    public List<NewsCategory>? Category { get; set; }

    public List<Language>? Languages { get; set; }

    public List<Country>? Countries { get; set; }

    public List<Weekdays> SendOnWeekdays { get; set; } = [];

    public List<TimeOnly> SendAtTimes { get; set; } = [];

    /// <summary>UTC instant when this row is next eligible for digest dispatch (aligned to one-minute slots).</summary>
    public DateTime? NextDigestSlotUtc { get; set; }

    /// <summary>Applies validated digest schedule windows (weekdays + times).</summary>
    public void AssignDigestSchedule(ScheduleWindow schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        schedule.ApplyToNews(this);
    }
}
