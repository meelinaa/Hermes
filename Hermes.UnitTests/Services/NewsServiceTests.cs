using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Specifications for news CRUD orchestration: reject invalid keys early; delegate valid operations to <see cref="IHermesDataStore"/>.
/// </summary>
public sealed class NewsServiceTests
{
    /// <summary>
    /// Null entity cannot be persisted.
    /// </summary>
    [Fact]
    public async Task SetNewsAsync_Should_Throw_WhenNewsNull()
    {
        NewsService sut = new(Mock.Of<IHermesDataStore>());

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
        NewsService sut = new(Mock.Of<IHermesDataStore>());
        News news = new() { Id = 0, UserId = invalidUserId };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.SetNewsAsync(news));
    }

    /// <summary>
    /// Returned id reflects whatever the store assigns during insert (callback simulates identity column).
    /// </summary>
    [Fact]
    public async Task SetNewsAsync_Should_ReturnPersistedId_AfterStoreAssignsKey()
    {
        News news = new() { Id = 0, UserId = 1 };
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.SetNewsAsync(It.IsAny<News>(), It.IsAny<CancellationToken>()))
            .Callback<News, CancellationToken>((n, _) => n.Id = 55)
            .Returns(Task.CompletedTask);

        NewsService sut = new(db.Object);

        int id = await sut.SetNewsAsync(news);

        Assert.Equal(55, id);
        db.Verify(x => x.SetNewsAsync(news, It.IsAny<CancellationToken>()), Times.Once);
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
        NewsService sut = new(Mock.Of<IHermesDataStore>());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetNewsByIdAsync(userId, newsId));
    }

    /// <summary>
    /// Valid ids delegate to store and return entity when present.
    /// </summary>
    [Fact]
    public async Task GetNewsByIdAsync_Should_ReturnEntity_FromStore_WhenIdentifiersValid()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetNewsByIdAsync(3, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new News { Id = 9, UserId = 3 });

        NewsService sut = new(db.Object);

        News? news = await sut.GetNewsByIdAsync(3, 9);

        Assert.NotNull(news);
        Assert.Equal(9, news!.Id);
    }

    /// <summary>
    /// Listing news by user requires positive user id.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-99)]
    public async Task GetAllNewsByUserAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        NewsService sut = new(Mock.Of<IHermesDataStore>());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.GetAllNewsByUserAsync(invalidUserId));
    }

    /// <summary>
    /// Bulk delete by user requires positive user id.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task DeleteAllNewsByUserAsync_Should_RejectNonPositiveUserId(int invalidUserId)
    {
        NewsService sut = new(Mock.Of<IHermesDataStore>());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.DeleteAllNewsByUserAsync(invalidUserId));
    }

    /// <summary>
    /// Delete-all returns the row count from persistence (pass-through).
    /// </summary>
    [Fact]
    public async Task DeleteAllNewsByUserAsync_Should_ReturnRemovedRowCount_FromStore()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.DeleteAllNewsByUserAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        NewsService sut = new(db.Object);

        Assert.Equal(7, await sut.DeleteAllNewsByUserAsync(4));
    }

    /// <summary>
    /// Update and delete delegate to store when entity reference is non-null.
    /// </summary>
    [Fact]
    public async Task UpdateNewsAsync_And_DeleteNewsAsync_Should_DelegateToStore_WhenEntitiesNonNull()
    {
        News news = new() { Id = 1, UserId = 1 };
        Mock<IHermesDataStore> db = new();

        NewsService sut = new(db.Object);

        await sut.UpdateNewsAsync(news);
        await sut.DeleteNewsAsync(news);

        db.Verify(x => x.UpdateNewsAsync(news, It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(x => x.DeleteNewsAsync(news, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateNewsAsync_Should_Throw_WhenNewsNull()
    {
        NewsService sut = new(Mock.Of<IHermesDataStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UpdateNewsAsync(null!));
    }

    [Fact]
    public async Task DeleteNewsAsync_Should_Throw_WhenNewsNull()
    {
        NewsService sut = new(Mock.Of<IHermesDataStore>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DeleteNewsAsync(null!));
    }
}
