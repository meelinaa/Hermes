using FluentResults;
using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Newsletter;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class NewsletterSubscriptionServiceTests
{
    private static readonly IOptions<NewsletterOptions> _defaultNewsletterOpts = Options.Create(new NewsletterOptions());

    [Fact]
    public async Task SetNewsAsync_Should_Fail_WhenNewsNull()
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);

        Result<NewsletterId> result = await sut.SetNewsAsync(null!);

        Assert.True(result.IsFailed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task SetNewsAsync_Should_Fail_WhenOwningUserIdNonPositive(int invalidUserId)
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);
        NewsletterSubscription news = NewsletterSubscription.CreateForUser(new UserId(1));
        news.SetUserId(new UserId(invalidUserId));
        news.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(10, 0)]));

        Result<NewsletterId> result = await sut.SetNewsAsync(news);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task SetNewsAsync_Should_ReturnPersistedId_AfterRepositoryAssignsKey()
    {
        NewsletterSubscription news = NewsletterSubscription.CreateForUser(new UserId(1));
        news.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(10, 0)]));
        Mock<INewsletterSubscriptionRepository> db = new();
        db.Setup(repository => repository.SetNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .Callback<NewsletterSubscription, CancellationToken>((n, _) => n.SetId(new NewsletterId(55)))
            .Returns(ValueTask.CompletedTask);
        db.Setup(dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<NewsletterId>(), It.IsAny<UserId>(), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, _defaultNewsletterOpts, TimeProvider.System);

        Result<NewsletterId> result = await sut.SetNewsAsync(news);

        Assert.True(result.IsSuccess);
        Assert.Equal(55, result.Value.Value);
        db.Verify(dataStore => dataStore.SetNewsAsync(news, It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(
            dataStore => dataStore.AdvanceNextDigestSlotAsync(
                new NewsletterId(55),
                new UserId(1),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-2, 5)]
    public async Task GetNewsByIdAsync_Should_Fail_WhenIdentifiersNonPositive(int userId, int newsId)
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);

        Result<NewsletterSubscription> result = await sut.GetNewsByIdAsync(new UserId(userId), new NewsletterId(newsId));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task DeleteNewsAsync_Should_RemoveFromStore_WithoutAdvancingDigestSlot()
    {
        Mock<INewsletterSubscriptionRepository> db = new();
        NewsletterSubscription news = NewsletterSubscription.CreateForUser(new UserId(4));
        news.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Tuesday], [new TimeOnly(8, 0)]));
        db.Setup(dataStore => dataStore.DeleteNewsAsync(news, It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, _defaultNewsletterOpts, TimeProvider.System);

        Result result = await sut.DeleteNewsAsync(news);

        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.DeleteNewsAsync(news, It.IsAny<CancellationToken>()), Times.Once);
        db.Verify(
            dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<NewsletterId>(),
                It.IsAny<UserId>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateNewsAsync_Should_AdvanceDigestSlot_AfterPersist()
    {
        NewsletterSubscription news = NewsletterSubscription.CreateForUser(new UserId(1));
        news.SetId(new NewsletterId(1));
        news.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(10, 0)]));
        Mock<INewsletterSubscriptionRepository> db = new();
        db.Setup(dataStore => dataStore.UpdateNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        db.Setup(dataStore => dataStore.AdvanceNextDigestSlotAsync(
                It.IsAny<NewsletterId>(),
                It.IsAny<UserId>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        NewsletterSubscriptionService sut = new(db.Object, _defaultNewsletterOpts, TimeProvider.System);

        Result result = await sut.UpdateNewsAsync(news);

        Assert.True(result.IsSuccess);
        db.Verify(dataStore => dataStore.AdvanceNextDigestSlotAsync(
            new NewsletterId(1),
            new UserId(1),
            It.IsAny<TimeZoneInfo>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-99)]
    public async Task GetNewsListAsync_Should_Fail_WhenUserIdNonPositive(int invalidUserId)
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);
        NewsletterSubscriptionListQueryDto query = new(invalidUserId, 1, 10, AfterId: null, SortDescending: false, Search: null, Category: null);

        Result<NewsletterSubscriptionListResultDto> result = await sut.GetNewsListAsync(query);

        Assert.True(result.IsFailed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public async Task DeleteAllNewsByUserAsync_Should_Fail_WhenUserIdNonPositive(int invalidUserId)
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);

        Result<int> result = await sut.DeleteAllNewsByUserAsync(new UserId(invalidUserId));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task UpdateNewsAsync_Should_Fail_WhenNewsNull()
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);

        Result result = await sut.UpdateNewsAsync(null!);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task DeleteNewsAsync_Should_Fail_WhenNewsNull()
    {
        NewsletterSubscriptionService sut = new(Mock.Of<INewsletterSubscriptionRepository>(), _defaultNewsletterOpts, TimeProvider.System);

        Result result = await sut.DeleteNewsAsync(null!);

        Assert.True(result.IsFailed);
    }
}
