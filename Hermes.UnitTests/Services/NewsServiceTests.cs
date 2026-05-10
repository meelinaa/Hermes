using Hermes.Application.Models.News;
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
/// Specifications for news CRUD orchestration: reject invalid keys early; delegate valid operations to <see cref="INewsStore"/>.
/// </summary>
public sealed class NewsServiceTests
{
    private static readonly IOptions<NewsletterOptions> DefaultNewsletterOpts = Options.Create(new NewsletterOptions());

    /// <summary>
    /// Null entity cannot be persisted.
    /// </summary>
    [Fact]
    public async Task SetNewsAsync_Should_Throw_WhenNewsNull()
    {
        NewsService sut = new(Mock.Of<INewsStore>(), DefaultNewsletterOpts);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SetNewsAsync(null!));
    }

    /// <summary>
    /// Owning user id must be positive before insert/update.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task SetNewsAsync_Should_RejectNonPositiveOwningUserId(int invalidUserId)
    {
        NewsService sut = new(Mock.Of<INewsStore>(), DefaultNewsletterOpts);
        News news = new() { Id = 0, UserId = invalidUserId };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.SetNewsAsync(news));
    }

    /// <summary>
    /// Returned id reflects whatever the store assigns during insert (callback simulates identity column).
    /// </summary>
    [Fact]
    public async Task SetNewsAsync_Should_ReturnPersistedId_AfterStoreAssignsKey()
    {
        News news = new() { Id = 0, UserId = 1, SendOnWeekdays = [Weekdays.Monday], SendAtTimes = [new TimeOnly(10, 0)] };
        Mock<INewsStore> db = new();
        db.Setup(dataStore => dataStore.SetNewsAsync(It.IsAny<News>(), It.IsAny<CancellationToken>()))
            .Callback<News, CancellationToken>((n, _) => n.Id = 55)
            .Returns(Task.CompletedTask);
        db.Setup(dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        NewsService sut = new(db.Object, DefaultNewsletterOpts);

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
    /// Both user id and news id must be positive for keyed reads.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-2, 5)]
    public async Task GetNewsByIdAsync_Should_RejectNonPositiveIdentifiers(int userId, int newsId)
    {
        NewsService sut = new(Mock.Of<INewsStore>(), DefaultNewsletterOpts);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetNewsByIdAsync(userId, newsId));
    }

    /// <summary>
    /// Valid ids delegate to store and return entity when present.
    /// </summary>
    [Fact]
    public async Task GetNewsByIdAsync_Should_ReturnEntity_FromStore_WhenIdentifiersValid()
    {
        Mock<INewsStore> db = new();
        db.Setup(dataStore => dataStore.GetNewsByIdAsync(3, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new News { Id = 9, UserId = 3 });

        NewsService sut = new(db.Object, DefaultNewsletterOpts);

        News? news = await sut.GetNewsByIdAsync(3, 9);

        Assert.NotNull(news);
        Assert.Equal(9, news!.Id);
    }

    /// <summary>
    /// Listing news requires positive user id on the query.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-99)]
    public async Task GetNewsListAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        NewsService sut = new(Mock.Of<INewsStore>(), DefaultNewsletterOpts);
        NewsListQuery query = new(invalidUserId, 1, 10, AfterId: null, SortDescending: false, Search: null, Category: null);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetNewsListAsync(query));
    }

    /// <summary>
    /// Valid query delegates to the data store.
    /// </summary>
    [Fact]
    public async Task GetNewsListAsync_Should_ReturnResult_FromStore()
    {
        NewsListResult expected = new(
            Items: [new News { Id = 1, UserId = 2 }],
            Page: 1,
            PageSize: 10,
            TotalCount: 1,
            TotalPages: 1,
            HasNextPage: false,
            NextAfterId: null);
        Mock<INewsStore> db = new();
        db.Setup(dataStore => dataStore.GetNewsListAsync(It.IsAny<NewsListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        NewsService sut = new(db.Object, DefaultNewsletterOpts);
        NewsListQuery q = new(2, 1, 10, null, false, null, NewsCategory.Technology);

        NewsListResult result = await sut.GetNewsListAsync(q);

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        db.Verify(dataStore => dataStore.GetNewsListAsync(q, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Bulk delete by user requires positive user id.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task DeleteAllNewsByUserAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        NewsService sut = new(Mock.Of<INewsStore>(), DefaultNewsletterOpts);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.DeleteAllNewsByUserAsync(invalidUserId));
    }

    /// <summary>
    /// Delete-all returns the row count from persistence (pass-through).
    /// </summary>
    [Fact]
    public async Task DeleteAllNewsByUserAsync_Should_ReturnRemovedRowCount_FromStore()
    {
        Mock<INewsStore> db = new();
        db.Setup(dataStore => dataStore.DeleteAllNewsByUserAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        NewsService sut = new(db.Object, DefaultNewsletterOpts);

        Assert.Equal(7, await sut.DeleteAllNewsByUserAsync(4));
    }

    /// <summary>
    /// Update and delete delegate to store when entity reference is non-null.
    /// </summary>
    [Fact]
    public async Task UpdateNewsAsync_And_DeleteNewsAsync_Should_DelegateToStore_WhenEntitiesNonNull()
    {
        News news = new() { Id = 1, UserId = 1, SendOnWeekdays = [Weekdays.Monday], SendAtTimes = [new TimeOnly(10, 0)] };
        Mock<INewsStore> db = new();
        db.Setup(dataStore => dataStore.UpdateNewsAsync(It.IsAny<News>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(dataStore => dataStore.DeleteNewsAsync(It.IsAny<News>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        db.Setup(dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        NewsService sut = new(db.Object, DefaultNewsletterOpts);
        await sut.UpdateNewsAsync(news);
        await sut.DeleteNewsAsync(news);

        db.Verify(dataStore => dataStore.UpdateNewsAsync(news, It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(dataStore => dataStore.DeleteNewsAsync(news, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateNewsAsync_Should_Throw_WhenNewsNull()
    {
        NewsService sut = new(Mock.Of<INewsStore>(), DefaultNewsletterOpts);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UpdateNewsAsync(null!));
    }

    [Fact]
    public async Task DeleteNewsAsync_Should_Throw_WhenNewsNull()
    {
        NewsService sut = new(Mock.Of<INewsStore>(), DefaultNewsletterOpts);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DeleteNewsAsync(null!));
    }
}
