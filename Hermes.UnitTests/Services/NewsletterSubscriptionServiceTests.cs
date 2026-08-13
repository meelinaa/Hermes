using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Newsletter;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="NewsletterSubscriptionService"/>.
/// </summary>
public sealed class NewsletterSubscriptionServiceTests
{
    private static readonly IOptions<NewsletterOptions> _defaultNewsletterOpts = Options.Create(new NewsletterOptions());

    // [E]RROR: Throws exception when input subscription is null
    /// <summary>
    /// Verifies that SetNewsAsync throws an ArgumentNullException if the subscription entity is null.
    /// </summary>
    [Fact]
    public async Task SetNewsAsync_Should_Throw_WhenNewsNull()
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.SetNewsAsync(null!));
    }

    // [B]OUNDARY: Non-positive owning user IDs are rejected
    /// <summary>
    /// Verifies that SetNewsAsync throws an ArgumentOutOfRangeException if the owning user ID is non-positive.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task SetNewsAsync_Should_RejectNonPositiveOwningUserId(int invalidUserId)
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);
        NewsletterSubscription news = NewsletterSubscription.CreateForUser(1);
        news.SetUserId(invalidUserId);
        news.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(10, 0)]));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.SetNewsAsync(news));
    }

    // [R]IGHT: Persists subscription entity and returns generated ID
    /// <summary>
    /// Verifies that SetNewsAsync correctly returns the persisted subscription ID and advances its scheduling slot.
    /// </summary>
    [Fact]
    public async Task SetNewsAsync_Should_ReturnPersistedId_AfterRepositoryAssignsKey()
    {
        // Arrange
        NewsletterSubscription news = NewsletterSubscription.CreateForUser(1);
        news.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(10, 0)]));
        Mock<INewsletterSubscriptionRepository> db = new();
        db.Setup(repository => repository.SetNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .Callback<NewsletterSubscription, CancellationToken>((n, _) => n.SetId(55))
            .Returns(ValueTask.CompletedTask);
        db.Setup(dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, _defaultNewsletterOpts, TimeProvider.System);

        // Act
        int id = await sut.SetNewsAsync(news);

        // Assert
        Assert.Equal(55, id);
        db.Verify(dataStore => dataStore.SetNewsAsync(news, It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(
            dataStore => dataStore.AdvanceNextDigestSlotAsync(
                55,
                1,
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // [B]OUNDARY: Non-positive user ID or news ID inputs are rejected
    /// <summary>
    /// Verifies that GetNewsByIdAsync rejects non-positive user and subscription identifiers.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-2, 5)]
    public async Task GetNewsByIdAsync_Should_RejectNonPositiveIdentifiers(int userId, int newsId)
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => await sut.GetNewsByIdAsync(userId, newsId));
    }

    // [R]IGHT: Deletes subscription from store without advancing digest slot
    /// <summary>
    /// Verifies that DeleteNewsAsync removes the subscription from the store without updating digest slots.
    /// </summary>
    [Fact]
    public async Task DeleteNewsAsync_Should_RemoveFromStore_WithoutAdvancingDigestSlot()
    {
        // Arrange
        Mock<INewsletterSubscriptionRepository> db = new();
        NewsletterSubscription news = NewsletterSubscription.CreateForUser(4);
        news.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Tuesday], [new TimeOnly(8, 0)]));
        db.Setup(dataStore => dataStore.DeleteNewsAsync(news, It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, _defaultNewsletterOpts, TimeProvider.System);

        // Act
        await sut.DeleteNewsAsync(news);

        // Assert
        db.Verify(dataStore => dataStore.DeleteNewsAsync(news, It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(
            dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // [R]IGHT: Updates subscription and advances next run slot
    /// <summary>
    /// Verifies that UpdateNewsAsync advances the next run slot after updating the subscription in the store.
    /// </summary>
    [Fact]
    public async Task UpdateNewsAsync_Should_AdvanceDigestSlot_AfterPersist()
    {
        // Arrange
        NewsletterSubscription news = NewsletterSubscription.CreateForUser(1);
        news.SetId(1);
        news.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(10, 0)]));
        Mock<INewsletterSubscriptionRepository> db = new();
        db.Setup(dataStore => dataStore.UpdateNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        db.Setup(dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, _defaultNewsletterOpts, TimeProvider.System);

        // Act
        await sut.UpdateNewsAsync(news);

        // Assert
        db.Verify(dataStore => dataStore.AdvanceNextDigestSlotAsync(
            1,
            1,
            It.IsAny<TimeZoneInfo>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // [B]OUNDARY: Rejects non-positive user ID query input
    /// <summary>
    /// Verifies that GetNewsListAsync rejects queries with non-positive user ID.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-99)]
    public async Task GetNewsListAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);
        NewsletterSubscriptionListQueryDto query = new(invalidUserId, 1, 10, AfterId: null, SortDescending: false, Search: null, Category: null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => await sut.GetNewsListAsync(query));
    }

    // [B]OUNDARY: Rejects non-positive user ID input for bulk deletion
    /// <summary>
    /// Verifies that DeleteAllNewsByUserAsync rejects non-positive user ID inputs.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task DeleteAllNewsByUserAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () => await sut.DeleteAllNewsByUserAsync(invalidUserId));
    }

    // [E]RROR: Throws exception when update payload is null
    /// <summary>
    /// Verifies that UpdateNewsAsync throws an ArgumentNullException if the subscription parameter is null.
    /// </summary>
    [Fact]
    public async Task UpdateNewsAsync_Should_Throw_WhenNewsNull()
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.UpdateNewsAsync(null!));
    }

    // [E]RROR: Throws exception when delete target is null
    /// <summary>
    /// Verifies that DeleteNewsAsync throws an ArgumentNullException if the subscription parameter is null.
    /// </summary>
    [Fact]
    public async Task DeleteNewsAsync_Should_Throw_WhenNewsNull()
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.DeleteNewsAsync(null!));
    }
}
