using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Xunit;

namespace Hermes.UnitTests.Domain.Entities;

public sealed class NewsletterSubscriptionTests
{
    [Fact]
    public void AssignDigestSchedule_Should_ApplyScheduleWindow()
    {
        // Arrange
        NewsletterSubscription sut = new();
        ScheduleWindow schedule = ScheduleWindow.EnsureForDigestScheduling([Weekdays.Friday], [new TimeOnly(12, 0)]);

        // Act
        sut.AssignDigestSchedule(schedule);

        // Assert
        Assert.Equal([Weekdays.Friday], sut.SendOnWeekdays);
        Assert.Equal([new TimeOnly(12, 0)], sut.SendAtTimes);
    }

    [Fact]
    public void AssignDigestSchedule_Should_ThrowArgumentNullException_WhenScheduleIsNull()
    {
        // Arrange
        NewsletterSubscription sut = new();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.AssignDigestSchedule(null!));
    }
}
