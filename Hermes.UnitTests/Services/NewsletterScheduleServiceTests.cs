using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Domain.Enums;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Specifications for <see cref="NewsletterScheduleService"/>: forwards local wall-clock to the data store’s due-slot query.
/// </summary>
public sealed class NewsletterScheduleServiceTests
{
    /// <summary>
    /// Fixed local Monday used across tests so weekday expectations stay stable (2026-01-05 is Monday in Gregorian calendar).
    /// </summary>
    private static DateTime MondayAt(int hour, int minute) =>
        new(2026, 1, 5, hour, minute, 0, DateTimeKind.Local);

    /// <summary>
    /// Empty result when the store returns nothing for that slot.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_ReturnEmpty_WhenStoreHasNoDueRowsForSlot()
    {
        Mock<INewsStore> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Monday,
                9,
                30,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result = await sut.GetDueItemsAsync(MondayAt(9, 30));

        Assert.Empty(result);
    }

    /// <summary>
    /// Returns whatever pairs the store computes for the derived weekday and clock slot.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_ReturnPairs_FromStore()
    {
        Mock<INewsStore> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Monday,
                9,
                30,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(42, 7), (43, 7)]);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result = await sut.GetDueItemsAsync(MondayAt(9, 30));

        Assert.Equal(2, result.Count);
        Assert.Contains((42, 7), result);
        Assert.Contains((43, 7), result);
    }

    /// <summary>
    /// Hour and minute from local <paramref name="nowLocal"/> flow into the store query.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_MapLocalClock_ToSlotParameters()
    {
        DateTime slot = new(2026, 1, 6, 14, 5, 0, DateTimeKind.Local); // Tuesday
        Mock<INewsStore> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Tuesday,
                14,
                5,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(1, 2)]);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result = await sut.GetDueItemsAsync(slot);

        Assert.Single(result);
        store.Verify(
            dataStore => dataStore.GetDueNewsScheduleForSlotAsync(Weekdays.Tuesday, 14, 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Cancellation token must flow to <see cref="INewsStore.GetDueNewsScheduleForSlotAsync"/> for cooperative cancellation.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_ForwardCancellation_ToDueSlotQuery()
    {
        Mock<INewsStore> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                It.IsAny<Weekdays>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        NewsletterScheduleService sut = new(store.Object);
        using CancellationTokenSource cts = new();

        await sut.GetDueItemsAsync(MondayAt(9, 30), cts.Token);

        store.Verify(
            dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Monday,
                9,
                30,
                cts.Token),
            Times.Once);
    }
}
