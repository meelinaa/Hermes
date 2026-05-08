using Hermes.Domain.Enums;

namespace Hermes.Domain.Mapping;

/// <summary>Converts between framework weekday values and Hermes weekday enum values.</summary>
public class WeekdayConverter
{
    /// <summary>Maps a local wall-clock date/time to the corresponding <see cref="Weekdays"/> value.</summary>
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
