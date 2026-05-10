using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Domain.Enums;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class NewsletterScheduleServiceTests
{
    private static DateTime MondayAt(int hour, int minute) =>
        new(2026, 1, 5, hour, minute, 0, DateTimeKind.Local);

    private static readonly DateTime SampleSlotStartUtc = new(2026, 6, 10, 7, 30, 0, DateTimeKind.Utc);

    private static readonly DateTime SampleSlotEndUtc = SampleSlotStartUtc.AddMinutes(1);

    [Fact]
    public async Task GetDueItemsAsync_Should_ReturnEmpty_WhenStoreHasNoDueRowsForSlot()
    {
        Mock<INewsStore> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Monday,
                9,
                30,
                SampleSlotStartUtc,
                SampleSlotEndUtc,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result =
            await sut.GetDueItemsAsync(MondayAt(9, 30), SampleSlotStartUtc, SampleSlotEndUtc);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetDueItemsAsync_Should_ReturnPairs_FromStore()
    {
        Mock<INewsStore> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Monday,
                9,
                30,
                SampleSlotStartUtc,
                SampleSlotEndUtc,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(42, 7), (43, 7)]);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result =
            await sut.GetDueItemsAsync(MondayAt(9, 30), SampleSlotStartUtc, SampleSlotEndUtc);

        Assert.Equal(2, result.Count);
        Assert.Contains((42, 7), result);
        Assert.Contains((43, 7), result);
    }

    [Fact]
    public async Task GetDueItemsAsync_Should_MapLocalClock_ToSlotParameters()
    {
        DateTime slot = new(2026, 1, 6, 14, 5, 0, DateTimeKind.Local);
        Mock<INewsStore> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Tuesday,
                14,
                5,
                SampleSlotStartUtc,
                SampleSlotEndUtc,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(1, 2)]);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result =
            await sut.GetDueItemsAsync(slot, SampleSlotStartUtc, SampleSlotEndUtc);

        Assert.Single(result);
        store.Verify(
            dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Tuesday, 14, 5, SampleSlotStartUtc, SampleSlotEndUtc, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDueItemsAsync_Should_ForwardCancellation_ToDueSlotQuery()
    {
        Mock<INewsStore> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                It.IsAny<Weekdays>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        NewsletterScheduleService sut = new(store.Object);
        using CancellationTokenSource cts = new();

        await sut.GetDueItemsAsync(MondayAt(9, 30), SampleSlotStartUtc, SampleSlotEndUtc, cts.Token);

        store.Verify(
            dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Monday,
                9,
                30,
                SampleSlotStartUtc,
                SampleSlotEndUtc,
                cts.Token),
            Times.Once);
    }
}
