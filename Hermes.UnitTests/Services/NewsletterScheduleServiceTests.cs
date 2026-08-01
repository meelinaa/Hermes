using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services;
using Hermes.Domain.Enums;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Unit tests for the <see cref="NewsletterScheduleService"/>.
/// </summary>
public sealed class NewsletterScheduleServiceTests
{
    /// <summary>
    /// Helper method to create a Monday local DateTime with specified hour and minute.
    /// </summary>
    private static DateTime MondayAt(int hour, int minute) =>
        new(2026, 1, 5, hour, minute, 0, DateTimeKind.Local);

    private static readonly DateTime _sampleSlotStartUtc = new(2026, 6, 10, 7, 30, 0, DateTimeKind.Utc);

    private static readonly DateTime _sampleSlotEndUtc = _sampleSlotStartUtc.AddMinutes(1);

    /// <summary>
    /// Verifies that GetDueItemsAsync returns an empty list if the store has no matching due subscriptions.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_ReturnEmpty_WhenStoreHasNoDueRowsForSlot()
    {
        Mock<INewsletterSubscriptionRepository> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Monday,
                9,
                30,
                SampleSlotStartUtc: _sampleSlotStartUtc,
                SampleSlotEndUtc: _sampleSlotEndUtc,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result =
            await sut.GetDueItemsAsync(MondayAt(9, 30), _sampleSlotStartUtc, _sampleSlotEndUtc);

        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that GetDueItemsAsync returns the due subscription pairs fetched from the store.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_ReturnPairs_FromStore()
    {
        Mock<INewsletterSubscriptionRepository> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Monday,
                9,
                30,
                SampleSlotStartUtc: _sampleSlotStartUtc,
                SampleSlotEndUtc: _sampleSlotEndUtc,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(42, 7), (43, 7)]);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result =
            await sut.GetDueItemsAsync(MondayAt(9, 30), _sampleSlotStartUtc, _sampleSlotEndUtc);

        Assert.Equal(2, result.Count);
        Assert.Contains((42, 7), result);
        Assert.Contains((43, 7), result);
    }

    /// <summary>
    /// Verifies that GetDueItemsAsync maps the input local time and weekdays correctly to the store slot parameters.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_MapLocalClock_ToSlotParameters()
    {
        DateTime slot = new(2026, 1, 6, 14, 5, 0, DateTimeKind.Local);
        Mock<INewsletterSubscriptionRepository> store = new();
        store.Setup(dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Tuesday,
                14,
                5,
                SampleSlotStartUtc: _sampleSlotStartUtc,
                SampleSlotEndUtc: _sampleSlotEndUtc,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([(1, 2)]);
        NewsletterScheduleService sut = new(store.Object);

        IReadOnlyList<(int NewsId, int UserId)> result =
            await sut.GetDueItemsAsync(slot, _sampleSlotStartUtc, _sampleSlotEndUtc);

        Assert.Single(result);
        store.Verify(
            dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Tuesday, 14, 5, _sampleSlotStartUtc, _sampleSlotEndUtc, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that GetDueItemsAsync correctly forwards the cancellation token to the store queries.
    /// </summary>
    [Fact]
    public async Task GetDueItemsAsync_Should_ForwardCancellation_ToDueSlotQuery()
    {
        Mock<INewsletterSubscriptionRepository> store = new();
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

        await sut.GetDueItemsAsync(MondayAt(9, 30), _sampleSlotStartUtc, _sampleSlotEndUtc, cts.Token);

        store.Verify(
            dataStore => dataStore.GetDueNewsScheduleForSlotAsync(
                Weekdays.Monday,
                9,
                30,
                _sampleSlotStartUtc,
                _sampleSlotEndUtc,
                cts.Token),
            Times.Once);
    }
}
