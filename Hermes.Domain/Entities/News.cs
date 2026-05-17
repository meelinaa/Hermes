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

    public bool IsEnabled { get; set; } = true;

    /// <summary>Materialized next digest eligibility (UTC minute boundary); query path may use JSON when unset.</summary>
    public DateTime? NextDigestSlotUtc { get; set; }
    public void AssignDigestSchedule(ScheduleWindow schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        schedule.ApplyToNews(this);
    }
}
