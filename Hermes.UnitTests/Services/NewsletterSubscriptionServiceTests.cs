using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Application.Options;
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

    /// <summary>
    /// Verifies that SetNewsAsync throws an ArgumentNullException if the subscription entity is null.
    /// </summary>
    // [E]RROR: Throws exception when input subscription is null
    [Fact]
    public async Task SetNewsAsync_Should_Throw_WhenNewsNull()
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetNewsAsync(null!));
    }

    /// <summary>
    /// Verifies that SetNewsAsync throws an ArgumentOutOfRangeException if the owning user ID is non-positive.
    /// </summary>
    // [B]OUNDARY: Non-positive owning user IDs are rejected
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task SetNewsAsync_Should_RejectNonPositiveOwningUserId(int invalidUserId)
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts);
        NewsletterSubscription news = new() { Id = 0, UserId = invalidUserId };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.SetNewsAsync(news));
    }

    /// <summary>
    /// Verifies that SetNewsAsync correctly returns the persisted subscription ID and advances its scheduling slot.
    /// </summary>
    // [R]IGHT: Persists subscription entity and returns generated ID
    [Fact]
    public async Task SetNewsAsync_Should_ReturnPersistedId_AfterRepositoryAssignsKey()
    {
        // Arrange
        NewsletterSubscription news = new() { Id = 0, UserId = 1, SendOnWeekdays = [Weekdays.Monday], SendAtTimes = [new TimeOnly(10, 0)] };
        Mock<INewsletterSubscriptionRepository> db = new();
        db.Setup(repository => repository.SetNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .Callback<NewsletterSubscription, CancellationToken>((n, _) => n.Id = 55)
            .Returns(Task.CompletedTask);
        db.Setup(dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, _defaultNewsletterOpts);

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

    /// <summary>
    /// Verifies that GetNewsByIdAsync rejects non-positive user and subscription identifiers.
    /// </summary>
    // [B]OUNDARY: Non-positive user ID or news ID inputs are rejected
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-2, 5)]
    public async Task GetNewsByIdAsync_Should_RejectNonPositiveIdentifiers(int userId, int newsId)
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetNewsByIdAsync(userId, newsId));
    }

    /// <summary>
    /// Verifies that DeleteNewsAsync removes the subscription from the store without updating digest slots.
    /// </summary>
    // [R]IGHT: Deletes subscription from store without advancing digest slot
    [Fact]
    public async Task DeleteNewsAsync_Should_RemoveFromStore_WithoutAdvancingDigestSlot()
    {
        // Arrange
        Mock<INewsletterSubscriptionRepository> db = new();
        NewsletterSubscription news = new()
        {
            Id = 9,
            UserId = 4,
            SendOnWeekdays = [Weekdays.Tuesday],
            SendAtTimes = [new TimeOnly(8, 0)],
        };
        db.Setup(dataStore => dataStore.DeleteNewsAsync(news, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, _defaultNewsletterOpts);

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

    /// <summary>
    /// Verifies that UpdateNewsAsync advances the next run slot after updating the subscription in the store.
    /// </summary>
    // [R]IGHT: Updates subscription and advances next run slot
    [Fact]
    public async Task UpdateNewsAsync_Should_AdvanceDigestSlot_AfterPersist()
    {
        // Arrange
        NewsletterSubscription news = new() { Id = 1, UserId = 1, SendOnWeekdays = [Weekdays.Monday], SendAtTimes = [new TimeOnly(10, 0)] };
        Mock<INewsletterSubscriptionRepository> db = new();
        db.Setup(dataStore => dataStore.UpdateNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, _defaultNewsletterOpts);

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

    /// <summary>
    /// Verifies that GetNewsListAsync rejects queries with non-positive user ID.
    /// </summary>
    // [B]OUNDARY: Rejects non-positive user ID query input
    [Theory]
    [InlineData(0)]
    [InlineData(-99)]
    public async Task GetNewsListAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts);
        NewsletterSubscriptionListQueryDto query = new(invalidUserId, 1, 10, AfterId: null, SortDescending: false, Search: null, Category: null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetNewsListAsync(query));
    }

    /// <summary>
    /// Verifies that DeleteAllNewsByUserAsync rejects non-positive user ID inputs.
    /// </summary>
    // [B]OUNDARY: Rejects non-positive user ID input for bulk deletion
    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task DeleteAllNewsByUserAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.DeleteAllNewsByUserAsync(invalidUserId));
    }

    /// <summary>
    /// Verifies that UpdateNewsAsync throws an ArgumentNullException if the subscription parameter is null.
    /// </summary>
    // [E]RROR: Throws exception when update payload is null
    [Fact]
    public async Task UpdateNewsAsync_Should_Throw_WhenNewsNull()
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UpdateNewsAsync(null!));
    }

    /// <summary>
    /// Verifies that DeleteNewsAsync throws an ArgumentNullException if the subscription parameter is null.
    /// </summary>
    // [E]RROR: Throws exception when delete target is null
    [Fact]
    public async Task DeleteNewsAsync_Should_Throw_WhenNewsNull()
    {
        // Arrange
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DeleteNewsAsync(null!));
    }
}
