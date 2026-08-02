namespace Hermes.Application.Scheduling;

/// <summary>
/// Provides utility methods for time zone resolution and wall-clock time conversions used in newsletter scheduling.
/// </summary>
public static class NewsletterSchedulingProvider
{
    /// <summary>
    /// Resolves the corresponding <see cref="TimeZoneInfo"/> from a given time zone identifier.
    /// Falls back to the local time zone if the identifier is null, whitespace, or invalid.
    /// </summary>
    /// <param name="timeZoneId">The time zone identifier to resolve.</param>
    /// <returns>The resolved <see cref="TimeZoneInfo"/>.</returns>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Local;

        string trimmed = timeZoneId.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out TimeZoneInfo? found) && found is not null)
            return found;

        return TimeZoneInfo.Local;
    }

    /// <summary>
    /// Gets the current date and time converted to the specified time zone.
    /// Used for determining local wall-clock execution times.
    /// </summary>
    /// <param name="zone">The target time zone.</param>
    /// <returns>The current wall-clock date and time in the given zone.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="zone"/> is null.</exception>
    public static DateTime GetWallClockNow(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        DateTimeOffset zoned = TimeZoneInfo.ConvertTime(utcNow, zone);
        return zoned.DateTime;
    }

    /// <summary>
    /// Gets the current date and time converted to the specified time zone, truncated to the start of the current minute.
    /// Used for aligning newsletter scheduling slots to minute boundaries.
    /// </summary>
    /// <param name="zone">The target time zone.</param>
    /// <returns>The current wall-clock date and time truncated to the minute.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="zone"/> is null.</exception>
    public static DateTime GetWallClockMinuteStart(TimeZoneInfo zone)
    {
        DateTime wallClockNow = GetWallClockNow(zone);
        return new DateTime(wallClockNow.Year, wallClockNow.Month, wallClockNow.Day, wallClockNow.Hour, wallClockNow.Minute, 0, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Converts a given wall-clock time in a specified time zone back to Coordinated Universal Time (UTC).
    /// Used for scheduling jobs in UTC while maintaining local wall-clock precision.
    /// </summary>
    /// <param name="wallMinuteStart">The local wall-clock time to convert.</param>
    /// <param name="zone">The time zone of the provided wall-clock time.</param>
    /// <returns>The equivalent UTC date and time.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="zone"/> is null.</exception>
    public static DateTime ConvertWallMinuteStartToUtc(DateTime wallMinuteStart, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return TimeZoneInfo.ConvertTimeToUtc(wallMinuteStart, zone);
    }
}
