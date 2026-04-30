using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Domain.DTOs;
using Hermes.Domain.Enums;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Specifications for computing which newsletter schedules are due: valid profiles only; weekday + configured minute;
/// no matches without a time window or with invalid ids.
/// </summary>
public sealed class NewsletterScheduleServiceTests
{
    /// <summary>
    /// Fixed local Monday used across tests so weekday expectations stay stable (2026-01-05 is Monday in Gregorian calendar).
    /// </summary>
    private static DateTime MondayAt(int hour, int minute) =>
        new(2026, 1, 5, hour, minute, 0, DateTimeKind.Local);

    /// <summary>
    /// Empty schedule query yields no due items.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_ReturnEmpty_WhenStoreHasNoRows()
    {
        // Arrange
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.GetNewsScheduleRowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        NewsletterScheduleService sut = new(store.Object);

        // Act
        IReadOnlyList<(int NewsId, int UserId)> result = await sut.GetDueItemsAsync(MondayAt(9, 30));

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    /// Rows with non-positive news id or user id are ignored (invalid linkage).
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_IgnoreRows_WithNonPositiveNewsOrUserId()
    {
        // Arrange
        List<NewsScheduleRow> rows =
        [
            new(0, 1, [Weekdays.Monday], [new TimeOnly(9, 30)]),
            new(1, 0, [Weekdays.Monday], [new TimeOnly(9, 30)]),
        ];
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.GetNewsScheduleRowsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        NewsletterScheduleService sut = new(store.Object);

        // Act
        IReadOnlyList<(int NewsId, int UserId)> result = await sut.GetDueItemsAsync(MondayAt(9, 30));

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    /// Current weekday must be included in the row's configured weekdays list.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_NotMatch_WhenWeekdayExcluded()
    {
        List<NewsScheduleRow> rows =
        [
            new(10, 2, [Weekdays.Tuesday], [new TimeOnly(9, 30)]),
        ];
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.GetNewsScheduleRowsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        NewsletterScheduleService sut = new(store.Object);

        Assert.Empty(await sut.GetDueItemsAsync(MondayAt(9, 30)));
    }

    /// <summary>
    /// At least one send time must be configured.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_NotMatch_WhenSendTimesEmpty()
    {
        List<NewsScheduleRow> rows =
        [
            new(10, 2, [Weekdays.Monday], []),
        ];
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.GetNewsScheduleRowsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        NewsletterScheduleService sut = new(store.Object);

        Assert.Empty(await sut.GetDueItemsAsync(MondayAt(9, 30)));
    }

    /// <summary>
    /// Current local minute must match one of the configured <see cref="TimeOnly"/> slots (hour + minute).
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_NotMatch_WhenSendTimesMissCurrentMinute()
    {
        List<NewsScheduleRow> rows =
        [
            new(10, 2, [Weekdays.Monday], [new TimeOnly(14, 0)]),
        ];
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.GetNewsScheduleRowsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        NewsletterScheduleService sut = new(store.Object);

        Assert.Empty(await sut.GetDueItemsAsync(MondayAt(9, 30)));
    }

    /// <summary>
    /// When weekday and minute align, return the (NewsId, UserId) pair exactly once per matching row.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_ReturnPair_WhenWeekdayAndMinuteMatch()
    {
        List<NewsScheduleRow> rows =
        [
            new(42, 7, [Weekdays.Monday], [new TimeOnly(9, 30)]),
        ];
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.GetNewsScheduleRowsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result = await sut.GetDueItemsAsync(MondayAt(9, 30));

        (int NewsId, int UserId) pair = Assert.Single(result);
        Assert.Equal(42, pair.NewsId);
        Assert.Equal(7, pair.UserId);
    }

    /// <summary>
    /// Multiple send times per row act as OR: current minute matching any slot yields a due item.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_MatchAnyConfiguredSendTime_WhenCurrentMinuteHitsThatSlot()
    {
        List<NewsScheduleRow> rows =
        [
            new(1, 99, [Weekdays.Monday], [new TimeOnly(8, 0), new TimeOnly(9, 30), new TimeOnly(12, 0)]),
        ];
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.GetNewsScheduleRowsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        NewsletterScheduleService sut = new(store.Object);

        Assert.Single(await sut.GetDueItemsAsync(MondayAt(9, 30)));
    }

    /// <summary>
    /// Different news profiles for the same user at the same slot produce separate due entries.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_ListSeparateProfiles_WhenSameSlotMultipleNewsRows()
    {
        List<NewsScheduleRow> rows =
        [
            new(1, 10, [Weekdays.Monday], [new TimeOnly(9, 30)]),
            new(2, 10, [Weekdays.Monday], [new TimeOnly(9, 30)]),
        ];
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.GetNewsScheduleRowsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result = await sut.GetDueItemsAsync(MondayAt(9, 30));

        Assert.Equal(2, result.Count);
        Assert.Contains((1, 10), result);
        Assert.Contains((2, 10), result);
    }

    /// <summary>
    /// Cancellation token must flow to <see cref="IHermesDataStore.GetNewsScheduleRowsAsync"/> for cooperative cancellation.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_ForwardCancellation_ToScheduleQuery()
    {
        // Arrange
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.GetNewsScheduleRowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        NewsletterScheduleService sut = new(store.Object);
        using CancellationTokenSource cts = new();

        // Act
        await sut.GetDueItemsAsync(MondayAt(9, 30), cts.Token);

        // Assert
        store.Verify(s => s.GetNewsScheduleRowsAsync(cts.Token), Times.Once);
    }
}
