using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Services;
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
    private static readonly IOptions<NewsletterOptions> DefaultNewsletterOpts = Options.Create(new NewsletterOptions());

    /// <summary>
    /// Verifies that SetNewsAsync throws an ArgumentNullException if the subscription entity is null.
    /// </summary>
    [Fact]
    public async Task SetNewsAsync_Should_Throw_WhenNewsNull()
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionStore>(), DefaultNewsletterOpts);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetNewsAsync(null!));
    }

    /// <summary>
    /// Verifies that SetNewsAsync throws an ArgumentOutOfRangeException if the owning user ID is non-positive.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task SetNewsAsync_Should_RejectNonPositiveOwningUserId(int invalidUserId)
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionStore>(), DefaultNewsletterOpts);
        NewsletterSubscription news = new() { Id = 0, UserId = invalidUserId };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.SetNewsAsync(news));
    }

    /// <summary>
    /// Verifies that SetNewsAsync correctly returns the persisted subscription ID and advances its scheduling slot.
    /// </summary>
    [Fact]
    public async Task SetNewsAsync_Should_ReturnPersistedId_AfterStoreAssignsKey()
    {
        NewsletterSubscription news = new() { Id = 0, UserId = 1, SendOnWeekdays = [Weekdays.Monday], SendAtTimes = [new TimeOnly(10, 0)] };
        Mock<INewsletterSubscriptionStore> db = new();
        db.Setup(dataStore => dataStore.SetNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .Callback<NewsletterSubscription, CancellationToken>((n, _) => n.Id = 55)
            .Returns(Task.CompletedTask);
        db.Setup(dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, DefaultNewsletterOpts);

        int id = await sut.SetNewsAsync(news);

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
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-2, 5)]
    public async Task GetNewsByIdAsync_Should_RejectNonPositiveIdentifiers(int userId, int newsId)
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionStore>(), DefaultNewsletterOpts);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetNewsByIdAsync(userId, newsId));
    }

    /// <summary>
    /// Verifies that DeleteNewsAsync removes the subscription from the store without updating digest slots.
    /// </summary>
    [Fact]
    public async Task DeleteNewsAsync_Should_RemoveFromStore_WithoutAdvancingDigestSlot()
    {
        Mock<INewsletterSubscriptionStore> db = new();
        NewsletterSubscription news = new()
        {
            Id = 9,
            UserId = 4,
            SendOnWeekdays = [Weekdays.Tuesday],
            SendAtTimes = [new TimeOnly(8, 0)],
        };
        db.Setup(dataStore => dataStore.DeleteNewsAsync(news, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, DefaultNewsletterOpts);
        await sut.DeleteNewsAsync(news);

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
    [Fact]
    public async Task UpdateNewsAsync_Should_AdvanceDigestSlot_AfterPersist()
    {
        NewsletterSubscription news = new() { Id = 1, UserId = 1, SendOnWeekdays = [Weekdays.Monday], SendAtTimes = [new TimeOnly(10, 0)] };
        Mock<INewsletterSubscriptionStore> db = new();
        db.Setup(dataStore => dataStore.UpdateNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, DefaultNewsletterOpts);
        await sut.UpdateNewsAsync(news);

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
    [Theory]
    [InlineData(0)]
    [InlineData(-99)]
    public async Task GetNewsListAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionStore>(), DefaultNewsletterOpts);
        NewsletterSubscriptionListQuery query = new(invalidUserId, 1, 10, AfterId: null, SortDescending: false, Search: null, Category: null);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetNewsListAsync(query));
    }

    /// <summary>
    /// Verifies that DeleteAllNewsByUserAsync rejects non-positive user ID inputs.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task DeleteAllNewsByUserAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionStore>(), DefaultNewsletterOpts);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.DeleteAllNewsByUserAsync(invalidUserId));
    }

    /// <summary>
    /// Verifies that UpdateNewsAsync throws an ArgumentNullException if the subscription parameter is null.
    /// </summary>
    [Fact]
    public async Task UpdateNewsAsync_Should_Throw_WhenNewsNull()
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionStore>(), DefaultNewsletterOpts);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UpdateNewsAsync(null!));
    }

    /// <summary>
    /// Verifies that DeleteNewsAsync throws an ArgumentNullException if the subscription parameter is null.
    /// </summary>
    [Fact]
    public async Task DeleteNewsAsync_Should_Throw_WhenNewsNull()
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionStore>(), DefaultNewsletterOpts);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DeleteNewsAsync(null!));
    }
}
