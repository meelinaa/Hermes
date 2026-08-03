using Hermes.Domain.Enums;

namespace Hermes.Application.Mapping;

public class WeekdayMapper
{
    public static Weekdays ToHermesWeekday(DateTime localWallClock) =>
      localWallClock.DayOfWeek switch
      {
          DayOfWeek.Monday => Weekdays.Monday,
          DayOfWeek.Tuesday => Weekdays.Tuesday,
          DayOfWeek.Wednesday => Weekdays.Wednesday,
          DayOfWeek.Thursday => Weekdays.Thursday,
          DayOfWeek.Friday => Weekdays.Friday,
          DayOfWeek.Saturday => Weekdays.Saturday,
          DayOfWeek.Sunday => Weekdays.Sunday,
          _ => Weekdays.Monday
      };
}
