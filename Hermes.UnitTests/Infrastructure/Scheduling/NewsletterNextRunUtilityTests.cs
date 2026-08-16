using Hermes.Domain.Enums;
using Hermes.Infrastructure.Adapters.Outbound.Scheduling;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.Scheduling;

/// <summary>
/// Contains unit tests for <see cref="NewsletterNextRunUtility"/>,
/// verifying UTC slot calculations over weekdays, time offsets, timezone boundaries, and DST transitions.
/// </summary>
public sealed class NewsletterNextRunUtilityTests
{
    private static readonly TimeZoneInfo _berlinZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "W. Europe Standard Time" : "Europe/Berlin");

    /// <summary>
    /// Tests that <see cref="NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter"/> calculates the next slot
    /// on the same day if a later configured time exists.
    /// </summary>
    [Fact]
    public void ComputeNextOccurrenceUtcAfter_Should_ReturnLaterTimeOnSameDay_WhenAvailable()
    {
        // Arrange
        Weekdays[] weekdays = [Weekdays.Monday];
        TimeOnly[] times = [new TimeOnly(8, 0), new TimeOnly(18, 0)];

        // Monday 08:00 Berlin (06:00 UTC in summer)
        DateTime monday8AmBerlinUtc = new(2026, 8, 17, 6, 0, 0, DateTimeKind.Utc);

        // Act
        DateTime? nextOccurrence = NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter(
            weekdays, times, _berlinZone, monday8AmBerlinUtc);

        // Assert (Expect same day at 18:00 Berlin = 16:00 UTC)
        Assert.NotNull(nextOccurrence);
        DateTime expectedUtc = new(2026, 8, 17, 16, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedUtc, nextOccurrence.Value);
        Assert.Equal(DateTimeKind.Utc, nextOccurrence.Value.Kind);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter"/> jumps to the next configured weekday
    /// when all configured times for the reference day have already passed.
    /// </summary>
    [Fact]
    public void ComputeNextOccurrenceUtcAfter_Should_AdvanceToNextConfiguredWeekday_WhenAllTodayTimesPassed()
    {
        // Arrange
        Weekdays[] weekdays = [Weekdays.Monday, Weekdays.Thursday];
        TimeOnly[] times = [new TimeOnly(9, 0)];

        // Monday 10:00 Berlin (08:00 UTC) -> Past today's 09:00 slot
        DateTime monday10AmBerlinUtc = new(2026, 8, 17, 8, 0, 0, DateTimeKind.Utc);

        // Act
        DateTime? nextOccurrence = NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter(
            weekdays, times, _berlinZone, monday10AmBerlinUtc);

        // Assert (Expect Thursday Aug 20 at 09:00 Berlin = 07:00 UTC)
        Assert.NotNull(nextOccurrence);
        DateTime expectedUtc = new(2026, 8, 20, 7, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedUtc, nextOccurrence.Value);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter"/> correctly handles
    /// reference dates provided with <see cref="DateTimeKind.Unspecified"/> or <see cref="DateTimeKind.Local"/>.
    /// </summary>
    [Fact]
    public void ComputeNextOccurrenceUtcAfter_Should_HandleNonUtcDateTimeKinds()
    {
        // Arrange
        Weekdays[] weekdays = [Weekdays.Wednesday];
        TimeOnly[] times = [new TimeOnly(12, 0)];
        DateTime unspecifiedRef = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Unspecified);

        // Act
        DateTime? nextOccurrence = NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter(
            weekdays, times, _berlinZone, unspecifiedRef);

        // Assert
        Assert.NotNull(nextOccurrence);
        Assert.Equal(DateTimeKind.Utc, nextOccurrence.Value.Kind);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter"/> crosses month and year boundaries correctly.
    /// </summary>
    [Fact]
    public void ComputeNextOccurrenceUtcAfter_Should_CrossYearBoundary()
    {
        // Arrange: Subscription runs on Fridays
        Weekdays[] weekdays = [Weekdays.Friday];
        TimeOnly[] times = [new TimeOnly(8, 0)];

        // Thursday Dec 31, 2026 23:00 UTC
        DateTime newYearsEveUtc = new(2026, 12, 31, 23, 0, 0, DateTimeKind.Utc);

        // Act
        DateTime? nextOccurrence = NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter(
            weekdays, times, _berlinZone, newYearsEveUtc);

        // Assert (Friday Jan 1, 2027 at 08:00 Berlin = 07:00 UTC in standard time)
        Assert.NotNull(nextOccurrence);
        DateTime expectedUtc = new(2027, 1, 1, 7, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expectedUtc, nextOccurrence.Value);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter"/> throws <see cref="ArgumentNullException"/>
    /// when the time zone is null.
    /// </summary>
    [Fact]
    public void ComputeNextOccurrenceUtcAfter_Should_ThrowArgumentNullException_WhenZoneIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter(
                [Weekdays.Monday],
                [new TimeOnly(8, 0)],
                null!,
                DateTime.UtcNow));
    }

    /// <summary>
    /// Tests that <see cref="NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter"/> throws <see cref="ArgumentException"/>
    /// when weekdays or times are empty.
    /// </summary>
    [Fact]
    public void ComputeNextOccurrenceUtcAfter_Should_ThrowArgumentException_WhenWeekdaysOrTimesEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter(
                [],
                [new TimeOnly(8, 0)],
                _berlinZone,
                DateTime.UtcNow));

        Assert.Throws<ArgumentException>(() =>
            NewsletterNextRunUtility.ComputeNextOccurrenceUtcAfter(
                [Weekdays.Monday],
                [],
                _berlinZone,
                DateTime.UtcNow));
    }
}
