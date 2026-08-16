using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Xunit;

namespace Hermes.UnitTests.Domain.Entities;

/// <summary>
/// Contains unit tests for the <see cref="NewsletterSubscription"/> domain entity,
/// validating lifecycle state mutations, filter sanitization, and digest schedule assignments.
/// </summary>
public sealed class NewsletterSubscriptionTests
{
    /// <summary>
    /// Tests that <see cref="NewsletterSubscription.CreateForUser"/> creates a subscription
    /// with the specified user ID and enables it by default.
    /// </summary>
    [Fact]
    public void CreateForUser_Should_SetUserIdAndEnableByDefault_WhenUserIdIsPositive()
    {
        // Arrange
        UserId userId = new(10);

        // Act
        NewsletterSubscription sut = NewsletterSubscription.CreateForUser(userId);

        // Assert
        Assert.Equal(userId, sut.UserId);
        Assert.True(sut.IsEnabled);
        Assert.Null(sut.Keywords);
        Assert.Null(sut.Category);
        Assert.Null(sut.Languages);
        Assert.Null(sut.Countries);
        Assert.Empty(sut.SendOnWeekdays);
        Assert.Empty(sut.SendAtTimes);
        Assert.Null(sut.NextDigestSlotUtc);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscription.CreateForUser"/> throws an <see cref="ArgumentOutOfRangeException"/>
    /// when the provided user ID is non-positive.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CreateForUser_Should_ThrowArgumentOutOfRangeException_WhenUserIdIsNonPositive(int invalidId)
    {
        // Arrange
        UserId userId = new(invalidId);

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => NewsletterSubscription.CreateForUser(userId));
        Assert.Contains("User ID must be positive", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscription.UpdateFilters"/> trims keyword strings
    /// and excludes null or whitespace entries, while storing categories, languages, and countries.
    /// </summary>
    [Fact]
    public void UpdateFilters_Should_TrimKeywordsAndFilterWhitespace_WhenProvided()
    {
        // Arrange
        NewsletterSubscription sut = NewsletterSubscription.CreateForUser(new UserId(1));
        string[] rawKeywords = ["  dotnet  ", "", "   ", "ai", " csharp "];
        NewsCategory[] categories = [NewsCategory.Technology, NewsCategory.Science];
        Language[] languages = [Language.German, Language.English];
        Country[] countries = [Country.Germany, Country.Austria];

        // Act
        sut.UpdateFilters(rawKeywords, categories, languages, countries);

        // Assert
        Assert.NotNull(sut.Keywords);
        Assert.Equal(["dotnet", "ai", "csharp"], sut.Keywords);
        Assert.Equal(categories, sut.Category);
        Assert.Equal(languages, sut.Languages);
        Assert.Equal(countries, sut.Countries);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscription.UpdateFilters"/> allows null collections
    /// and resets the respective properties to null.
    /// </summary>
    [Fact]
    public void UpdateFilters_Should_AllowNullCollections()
    {
        // Arrange
        NewsletterSubscription sut = NewsletterSubscription.CreateForUser(new UserId(1));
        sut.UpdateFilters(["test"], [NewsCategory.Technology], [Language.English], [Country.Germany]);

        // Act
        sut.UpdateFilters(null, null, null, null);

        // Assert
        Assert.Null(sut.Keywords);
        Assert.Null(sut.Category);
        Assert.Null(sut.Languages);
        Assert.Null(sut.Countries);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscription.Disable"/> and <see cref="NewsletterSubscription.Enable"/>
    /// properly toggle the <see cref="NewsletterSubscription.IsEnabled"/> flag.
    /// </summary>
    [Fact]
    public void EnableAndDisable_Should_ToggleIsEnabledState()
    {
        // Arrange
        NewsletterSubscription sut = NewsletterSubscription.CreateForUser(new UserId(1));
        Assert.True(sut.IsEnabled);

        // Act & Assert Disable
        sut.Disable();
        Assert.False(sut.IsEnabled);

        // Act & Assert Enable
        sut.Enable();
        Assert.True(sut.IsEnabled);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscription.SetNextDigestSlot"/> updates the materialized next digest execution instant.
    /// </summary>
    [Fact]
    public void SetNextDigestSlot_Should_UpdateNextDigestSlotUtc()
    {
        // Arrange
        NewsletterSubscription sut = NewsletterSubscription.CreateForUser(new UserId(1));
        DateTime expectedSlot = new(2026, 8, 17, 8, 0, 0, DateTimeKind.Utc);

        // Act
        sut.SetNextDigestSlot(expectedSlot);

        // Assert
        Assert.Equal(expectedSlot, sut.NextDigestSlotUtc);

        // Act - Reset to null
        sut.SetNextDigestSlot(null);

        // Assert
        Assert.Null(sut.NextDigestSlotUtc);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscription.AssignDigestSchedule"/> applies the schedule window's
    /// configured weekdays and times to the subscription.
    /// </summary>
    [Fact]
    public void AssignDigestSchedule_Should_ApplyScheduleWindow()
    {
        // Arrange
        NewsletterSubscription sut = NewsletterSubscription.CreateForUser(new UserId(1));
        ScheduleWindow schedule = ScheduleWindow.EnsureForDigestScheduling([Weekdays.Friday], [new TimeOnly(12, 0)]);

        // Act
        sut.AssignDigestSchedule(schedule);

        // Assert
        Assert.Equal([Weekdays.Friday], sut.SendOnWeekdays);
        Assert.Equal([new TimeOnly(12, 0)], sut.SendAtTimes);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscription.AssignDigestSchedule"/> throws an <see cref="ArgumentNullException"/>
    /// when the passed schedule window is null.
    /// </summary>
    [Fact]
    public void AssignDigestSchedule_Should_ThrowArgumentNullException_WhenScheduleIsNull()
    {
        // Arrange
        NewsletterSubscription sut = NewsletterSubscription.CreateForUser(new UserId(1));

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.AssignDigestSchedule(null!));
    }
}
