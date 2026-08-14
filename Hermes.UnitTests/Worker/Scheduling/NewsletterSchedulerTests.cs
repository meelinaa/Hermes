using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports.Inbound;
using Hermes.Domain.ValueObjects;
using Hermes.Worker.Services.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Worker.Scheduling;

public sealed class NewsletterSchedulerTests
{
    [Fact]
    public async Task RunAsync_Should_QueryDueProfilesOnce_WhenNothingDue()
    {
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(scheduleService => scheduleService.GetDueItemsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(NewsletterId NewsId, UserId UserId)>());

        NewsletterSchedulerWorkerService sut = new(
            schedule.Object,
            new Mock<global::Hangfire.IBackgroundJobClient>().Object,
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            Options.Create(new NewsletterOptions()));
        await sut.RunAsync();
        schedule.Verify(
            scheduleService => scheduleService.GetDueItemsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_Should_ForwardSameCancellationToken_ToScheduleService()
    {
        CancellationToken? captured = null;
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(scheduleService => scheduleService.GetDueItemsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Callback<DateTime, DateTime, DateTime, CancellationToken>((_, _, _, ct) => captured = ct)
            .ReturnsAsync([]);

        NewsletterSchedulerWorkerService sut = new(
            schedule.Object,
            new Mock<global::Hangfire.IBackgroundJobClient>().Object,
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            Options.Create(new NewsletterOptions()));

        using CancellationTokenSource cts = new();
        await sut.RunAsync(cts.Token);
        Assert.True(captured.HasValue);
        Assert.Equal(cts.Token, captured.Value);
    }

    [Fact]
    public async Task RunAsync_Should_EnqueueJobs_ForEveryDueItem()
    {
        // Arrange
        IReadOnlyList<(NewsletterId NewsId, UserId UserId)> dueItems = [(new NewsletterId(10), new UserId(1)), (new NewsletterId(11), new UserId(2))];
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(s => s.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dueItems);

        Mock<global::Hangfire.IBackgroundJobClient> jobClient = new();

        NewsletterSchedulerWorkerService sut = new(
            schedule.Object,
            jobClient.Object,
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            Options.Create(new NewsletterOptions()));

        // Act
        await sut.RunAsync();

        // Assert
        jobClient.Verify(
            x => x.Create(
                It.Is<global::Hangfire.Common.Job>(j => j.Type == typeof(Hermes.Application.Services.NotificationLogs.NotificationJobService) && j.Method.Name == nameof(Hermes.Application.Services.NotificationLogs.NotificationJobService.SendNewsDigestAsync)),
                It.IsAny<global::Hangfire.States.EnqueuedState>()),
            Times.Exactly(2));
    }
}
