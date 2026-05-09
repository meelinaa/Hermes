namespace Hermes.Application.Scheduling;

/// <summary>Wall-clock time used to match stored <see cref="TimeOnly"/> send slots against “now” in the configured newsletter time zone.</summary>
public static class NewsletterSchedulingClock
{
    /// <summary>Resolves <paramref name="timeZoneId"/> or falls back to <see cref="TimeZoneInfo.Local"/> when empty or unknown.</summary>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Local;

        string trimmed = timeZoneId.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out TimeZoneInfo? found) && found is not null)
            return found;

        return TimeZoneInfo.Local;
    }

    /// <summary>Current instant expressed in <paramref name="zone"/> (for weekday, hour, and minute).</summary>
    public static DateTime GetWallClockNow(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        DateTimeOffset zoned = TimeZoneInfo.ConvertTime(utcNow, zone);
        return zoned.DateTime;
    }

    /// <summary>Start of the current clock minute in <paramref name="zone"/> (<see cref="DateTimeKind.Unspecified"/>).</summary>
    public static DateTime GetWallClockMinuteStart(TimeZoneInfo zone)
    {
        DateTime w = GetWallClockNow(zone);
        return new DateTime(w.Year, w.Month, w.Day, w.Hour, w.Minute, 0, DateTimeKind.Unspecified);
    }

    /// <summary>UTC instant for the start of a wall-clock minute in <paramref name="zone"/>.</summary>
    public static DateTime WallMinuteStartToUtc(DateTime wallMinuteStart, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return TimeZoneInfo.ConvertTimeToUtc(wallMinuteStart, zone);
    }
}
