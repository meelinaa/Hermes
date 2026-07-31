namespace Hermes.Application.Scheduling;

public static class NewsletterSchedulingProvider
{
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return TimeZoneInfo.Local;

        string trimmed = timeZoneId.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out TimeZoneInfo? found) && found is not null)
            return found;

        return TimeZoneInfo.Local;
    }

    public static DateTime GetWallClockNow(TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        DateTimeOffset zoned = TimeZoneInfo.ConvertTime(utcNow, zone);
        return zoned.DateTime;
    }

    public static DateTime GetWallClockMinuteStart(TimeZoneInfo zone)
    {
        DateTime w = GetWallClockNow(zone);
        return new DateTime(w.Year, w.Month, w.Day, w.Hour, w.Minute, 0, DateTimeKind.Unspecified);
    }

    public static DateTime WallMinuteStartToUtc(DateTime wallMinuteStart, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return TimeZoneInfo.ConvertTimeToUtc(wallMinuteStart, zone);
    }
}
