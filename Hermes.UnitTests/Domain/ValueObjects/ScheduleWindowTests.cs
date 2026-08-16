using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Xunit;

namespace Hermes.UnitTests.Domain.ValueObjects;

/// <summary>
/// Contains unit tests for the <see cref="ScheduleWindow"/> value object,
/// testing scheduling invariants, collection sanitization, and application to domain entities.
/// </summary>
public sealed class ScheduleWindowTests
{
    /// <summary>
    /// Tests that <see cref="ScheduleWindow.EnsureForDigestScheduling"/> deduplicates and sorts weekdays and times.
    /// </summary>
    [Fact]
    public void EnsureForDigestScheduling_Should_ReturnDistinctAndSortedCollections()
    {
        // Arrange
        List<Weekdays> days = [Weekdays.Sunday, Weekdays.Monday, Weekdays.Monday, Weekdays.Saturday];
        List<TimeOnly> times = [new TimeOnly(12, 0), new TimeOnly(8, 0), new TimeOnly(12, 0)];

        // Act
        ScheduleWindow result = ScheduleWindow.EnsureForDigestScheduling(days, times);

        // Assert
        Assert.Equal([Weekdays.Monday, Weekdays.Saturday, Weekdays.Sunday], result.Weekdays);
        Assert.Equal([new TimeOnly(8, 0), new TimeOnly(12, 0)], result.Times);
    }

    /// <summary>
    /// Tests that <see cref="ScheduleWindow.EnsureForDigestScheduling"/> throws a <see cref="DomainValidationException"/>
    /// when the weekdays collection is null.
    /// </summary>
    [Fact]
    public void EnsureForDigestScheduling_Should_ThrowDomainValidationException_WhenWeekdaysIsNull()
    {
        // Arrange
        List<TimeOnly> times = [new TimeOnly(8, 0)];

        // Act & Assert
        DomainValidationException ex = Assert.Throws<DomainValidationException>(() => ScheduleWindow.EnsureForDigestScheduling(null, times));
        Assert.Contains("requires at least one weekday", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="ScheduleWindow.EnsureForDigestScheduling"/> throws a <see cref="DomainValidationException"/>
    /// when the times collection is null.
    /// </summary>
    [Fact]
    public void EnsureForDigestScheduling_Should_ThrowDomainValidationException_WhenTimesIsNull()
    {
        // Arrange
        List<Weekdays> days = [Weekdays.Monday];

        // Act & Assert
        DomainValidationException ex = Assert.Throws<DomainValidationException>(() => ScheduleWindow.EnsureForDigestScheduling(days, null));
        Assert.Contains("requires at least one weekday", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="ScheduleWindow.EnsureForDigestScheduling"/> throws a <see cref="DomainValidationException"/>
    /// when the weekdays collection is empty.
    /// </summary>
    [Fact]
    public void EnsureForDigestScheduling_Should_ThrowDomainValidationException_WhenWeekdaysIsEmpty()
    {
        // Arrange
        List<Weekdays> days = [];
        List<TimeOnly> times = [new TimeOnly(8, 0)];

        // Act & Assert
        DomainValidationException ex = Assert.Throws<DomainValidationException>(() => ScheduleWindow.EnsureForDigestScheduling(days, times));
        Assert.Contains("requires at least one weekday", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="ScheduleWindow.EnsureForDigestScheduling"/> throws a <see cref="DomainValidationException"/>
    /// when the times collection is empty.
    /// </summary>
    [Fact]
    public void EnsureForDigestScheduling_Should_ThrowDomainValidationException_WhenTimesIsEmpty()
    {
        // Arrange
        List<Weekdays> days = [Weekdays.Monday];
        List<TimeOnly> times = [];

        // Act & Assert
        DomainValidationException ex = Assert.Throws<DomainValidationException>(() => ScheduleWindow.EnsureForDigestScheduling(days, times));
        Assert.Contains("requires at least one weekday", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="ScheduleWindow.ApplyToSubscription"/> assigns the configured weekdays and times to a newsletter subscription entity.
    /// </summary>
    [Fact]
    public void ApplyToSubscription_Should_UpdateSubscriptionCollections()
    {
        // Arrange
        List<Weekdays> days = [Weekdays.Friday];
        List<TimeOnly> times = [new TimeOnly(18, 0)];
        ScheduleWindow sut = ScheduleWindow.EnsureForDigestScheduling(days, times);
        NewsletterSubscription sub = NewsletterSubscription.CreateForUser(new UserId(1));

        // Act
        sut.ApplyToSubscription(sub);

        // Assert
        Assert.Equal(days, sub.SendOnWeekdays);
        Assert.Equal(times, sub.SendAtTimes);
    }

    /// <summary>
    /// Tests that <see cref="ScheduleWindow.ApplyToSubscription"/> throws an <see cref="ArgumentNullException"/>
    /// when the target subscription is null.
    /// </summary>
    [Fact]
    public void ApplyToSubscription_Should_ThrowArgumentNullException_WhenSubscriptionIsNull()
    {
        // Arrange
        ScheduleWindow sut = ScheduleWindow.EnsureForDigestScheduling([Weekdays.Friday], [new TimeOnly(18, 0)]);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.ApplyToSubscription(null!));
    }
}
