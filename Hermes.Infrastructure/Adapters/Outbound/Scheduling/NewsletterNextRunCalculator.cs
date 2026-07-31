using Hermes.Application.Mapping;
using Hermes.Domain.Enums;

namespace Hermes.Infrastructure.Adapters.Outbound.Scheduling;

/// <summary>Computes the next UTC instant for a newsletter digest given weekday/time-of-day rules in a time zone.</summary>
public static class NewsletterNextRunCalculator
{
    /// <summary>Returns the earliest scheduled send strictly after <paramref name="referenceUtcExclusive"/>.</summary>
    public static DateTime ComputeNextOccurrenceUtcAfter(
        IReadOnlyList<Weekdays> weekdays,
        IReadOnlyList<TimeOnly> times,
        TimeZoneInfo zone,
        DateTime referenceUtcExclusive)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (weekdays.Count == 0 || times.Count == 0)
            throw new ArgumentException("Schedule requires at least one weekday and one send time.");

        DateTime refUtc = referenceUtcExclusive.Kind == DateTimeKind.Utc
            ? referenceUtcExclusive
            : referenceUtcExclusive.ToUniversalTime();
        refUtc = DateTime.SpecifyKind(refUtc, DateTimeKind.Utc);

        DateTime refLocal = TimeZoneInfo.ConvertTimeFromUtc(refUtc, zone);
        DateTime? bestUtc = null;

        for (int dayOffset = 0; dayOffset < 400; dayOffset++)
        {
            DateTime civilMidnight = refLocal.Date.AddDays(dayOffset);
            DateTime localNoon = civilMidnight.AddHours(12);
            Weekdays wd = WeekdayConverter.ToHermesWeekday(localNoon);
            if (!weekdays.Contains(wd))
                continue;

            foreach (TimeOnly to in times)
            {
                DateTime localCandidate = civilMidnight.Date + to.ToTimeSpan();
                DateTime utcCandidate = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(localCandidate, DateTimeKind.Unspecified),
                    zone);
                if (utcCandidate > refUtc && (bestUtc is null || utcCandidate < bestUtc.Value))
                    bestUtc = utcCandidate;
            }
        }

        if (bestUtc is null)
            throw new InvalidOperationException("Could not compute the next newsletter digest slot.");

        return DateTime.SpecifyKind(bestUtc.Value, DateTimeKind.Utc);
    }
}
