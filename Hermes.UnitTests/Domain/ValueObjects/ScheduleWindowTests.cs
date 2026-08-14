using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Xunit;
using Hermes.Domain.Exceptions;

namespace Hermes.UnitTests.Domain.ValueObjects;

public sealed class ScheduleWindowTests
{
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

    [Fact]
    public void EnsureForDigestScheduling_Should_ThrowArgumentException_WhenWeekdaysIsNull()
    {
        // Arrange
        List<TimeOnly> times = [new TimeOnly(8, 0)];

        // Act & Assert
        DomainValidationException ex = Assert.Throws<DomainValidationException>(() => ScheduleWindow.EnsureForDigestScheduling(null, times));
        Assert.Contains("requires at least one weekday", ex.Message);
    }

    [Fact]
    public void EnsureForDigestScheduling_Should_ThrowArgumentException_WhenTimesIsNull()
    {
        // Arrange
        List<Weekdays> days = [Weekdays.Monday];

        // Act & Assert
        DomainValidationException ex = Assert.Throws<DomainValidationException>(() => ScheduleWindow.EnsureForDigestScheduling(days, null));
        Assert.Contains("requires at least one weekday", ex.Message);
    }

    [Fact]
    public void EnsureForDigestScheduling_Should_ThrowArgumentException_WhenWeekdaysIsEmpty()
    {
        // Arrange
        List<Weekdays> days = [];
        List<TimeOnly> times = [new TimeOnly(8, 0)];

        // Act & Assert
        DomainValidationException ex = Assert.Throws<DomainValidationException>(() => ScheduleWindow.EnsureForDigestScheduling(days, times));
        Assert.Contains("requires at least one weekday", ex.Message);
    }

    [Fact]
    public void EnsureForDigestScheduling_Should_ThrowArgumentException_WhenTimesIsEmpty()
    {
        // Arrange
        List<Weekdays> days = [Weekdays.Monday];
        List<TimeOnly> times = [];

        // Act & Assert
        DomainValidationException ex = Assert.Throws<DomainValidationException>(() => ScheduleWindow.EnsureForDigestScheduling(days, times));
        Assert.Contains("requires at least one weekday", ex.Message);
    }

    [Fact]
    public void ApplyToSubscription_Should_UpdateSubscriptionCollections()
    {
        // Arrange
        List<Weekdays> days = [Weekdays.Friday];
        List<TimeOnly> times = [new TimeOnly(18, 0)];
        ScheduleWindow sut = ScheduleWindow.EnsureForDigestScheduling(days, times);
        NewsletterSubscription sub = new();

        // Act
        sut.ApplyToSubscription(sub);

        // Assert
        Assert.Equal(days, sub.SendOnWeekdays);
        Assert.Equal(times, sub.SendAtTimes);
    }

    [Fact]
    public void ApplyToSubscription_Should_ThrowArgumentNullException_WhenSubscriptionIsNull()
    {
        // Arrange
        ScheduleWindow sut = ScheduleWindow.EnsureForDigestScheduling([Weekdays.Friday], [new TimeOnly(18, 0)]);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.ApplyToSubscription(null!));
    }
}
