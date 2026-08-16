using Hermes.Application.Services.Newsletter;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Contains unit tests for <see cref="NewsletterSchedulingProvider"/>,
/// testing timezone resolution, wall-clock time alignment, and UTC conversions.
/// </summary>
public sealed class NewsletterSchedulingProviderTests
{
    /// <summary>
    /// Tests that <see cref="NewsletterSchedulingProvider.ResolveTimeZone"/> falls back to <see cref="TimeZoneInfo.Local"/>
    /// when the time zone identifier is null, empty, whitespace, or invalid.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NonExistent/Invalid_TimeZone_Id_12345")]
    public void ResolveTimeZone_Should_FallbackToLocal_WhenIdentifierNullOrInvalid(string? invalidId)
    {
        // Act
        TimeZoneInfo result = NewsletterSchedulingProvider.ResolveTimeZone(invalidId);

        // Assert
        Assert.Equal(TimeZoneInfo.Local.Id, result.Id);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSchedulingProvider.ResolveTimeZone"/> resolves standard time zone IDs.
    /// </summary>
    [Fact]
    public void ResolveTimeZone_Should_ResolveValidTimeZoneId()
    {
        // Act
        TimeZoneInfo result = NewsletterSchedulingProvider.ResolveTimeZone("UTC");

        // Assert
        Assert.Equal(TimeZoneInfo.Utc.Id, result.Id);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSchedulingProvider.GetWallClockNow"/> returns the current date/time
    /// converted to the given time zone and throws <see cref="ArgumentNullException"/> when zone is null.
    /// </summary>
    [Fact]
    public void GetWallClockNow_Should_ReturnTimeInTargetZone_AndValidateArguments()
    {
        // Act & Assert Null
        Assert.Throws<ArgumentNullException>(() => NewsletterSchedulingProvider.GetWallClockNow(null!));

        // Act Valid
        DateTime wallNowUtc = NewsletterSchedulingProvider.GetWallClockNow(TimeZoneInfo.Utc);
        DateTime utcNow = DateTime.UtcNow;

        // Assert
        Assert.True(Math.Abs((wallNowUtc - utcNow).TotalSeconds) < 2);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSchedulingProvider.GetWallClockMinuteStart"/> truncates seconds and milliseconds to zero.
    /// </summary>
    [Fact]
    public void GetWallClockMinuteStart_Should_TruncateToMinuteBoundary()
    {
        // Act & Assert Null
        Assert.Throws<ArgumentNullException>(() => NewsletterSchedulingProvider.GetWallClockMinuteStart(null!));

        // Act Valid
        DateTime minuteStart = NewsletterSchedulingProvider.GetWallClockMinuteStart(TimeZoneInfo.Utc);

        // Assert
        Assert.Equal(0, minuteStart.Second);
        Assert.Equal(0, minuteStart.Millisecond);
        Assert.Equal(DateTimeKind.Unspecified, minuteStart.Kind);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSchedulingProvider.ConvertWallMinuteStartToUtc"/> converts wall clock time to UTC.
    /// </summary>
    [Fact]
    public void ConvertWallMinuteStartToUtc_Should_ConvertToUtcProperly()
    {
        // Act & Assert Null
        Assert.Throws<ArgumentNullException>(() =>
            NewsletterSchedulingProvider.ConvertWallMinuteStartToUtc(DateTime.Now, null!));

        // Act Valid (UTC zone test)
        DateTime localWall = new(2026, 8, 16, 14, 30, 0, DateTimeKind.Unspecified);
        DateTime converted = NewsletterSchedulingProvider.ConvertWallMinuteStartToUtc(localWall, TimeZoneInfo.Utc);

        // Assert
        Assert.Equal(localWall, converted);
    }
}
