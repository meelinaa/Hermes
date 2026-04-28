using Hermes.Domain.Enums;

namespace Hermes.Domain.Mapping;

public class WeekdayConverter
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
