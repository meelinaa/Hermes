using Hangfire;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports.Inbound;
using Hermes.Domain.ValueObjects;
using Hermes.Worker.Services.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Worker.Scheduling;

/// <summary>
/// Contains unit tests for <see cref="NewsletterSchedulerWorkerService"/>,
/// verifying minute-by-minute scheduling loops, background job enqueuing, and cancellation token forwarding.
/// </summary>
public sealed class NewsletterSchedulerTests
{
    /// <summary>
    /// Tests that <see cref="NewsletterSchedulerWorkerService.RunAsync"/> queries for due items
    /// and completes gracefully when no items are scheduled.
    /// </summary>
    [Fact]
    public async Task RunAsync_Should_QueryDueProfilesOnce_WhenNothingDue()
    {
        // Arrange
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(scheduleService => scheduleService.GetDueItemsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(NewsletterId NewsId, UserId UserId)>());

        NewsletterSchedulerWorkerService sut = new(
            schedule.Object,
            Mock.Of<IBackgroundJobClient>(),
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            Options.Create(new NewsletterOptions()));

        // Act
        await sut.RunAsync();

        // Assert
        schedule.Verify(
            scheduleService => scheduleService.GetDueItemsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSchedulerWorkerService.RunAsync"/> forwards the caller's cancellation token
    /// to the underlying schedule evaluation service.
    /// </summary>
    [Fact]
    public async Task RunAsync_Should_ForwardSameCancellationToken_ToScheduleService()
    {
        // Arrange
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
            Mock.Of<IBackgroundJobClient>(),
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            Options.Create(new NewsletterOptions()));

        using CancellationTokenSource cts = new();

        // Act
        await sut.RunAsync(cts.Token);

        // Assert
        Assert.True(captured.HasValue);
        Assert.Equal(cts.Token, captured.Value);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSchedulerWorkerService.RunAsync"/> enqueues a Hangfire job
    /// for every due newsletter profile returned by the schedule service.
    /// </summary>
    [Fact]
    public async Task RunAsync_Should_EnqueueJobs_ForEveryDueItem()
    {
        // Arrange
        IReadOnlyList<(NewsletterId NewsId, UserId UserId)> dueItems =
        [
            (new NewsletterId(10), new UserId(1)),
            (new NewsletterId(11), new UserId(2))
        ];
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(s => s.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dueItems);

        Mock<IBackgroundJobClient> jobClient = new();

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
                It.Is<global::Hangfire.Common.Job>(j =>
                    j.Type == typeof(Hermes.Application.Services.NotificationLogs.NotificationJobService) &&
                    j.Method.Name == nameof(Hermes.Application.Services.NotificationLogs.NotificationJobService.SendNewsDigestAsync)),
                It.IsAny<global::Hangfire.States.EnqueuedState>()),
            Times.Exactly(2));
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSchedulerWorkerService.RunAsync"/> propagates exceptions
    /// thrown by the underlying scheduling service.
    /// </summary>
    [Fact]
    public async Task RunAsync_Should_PropagateExceptions_WhenScheduleServiceFails()
    {
        // Arrange
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(s => s.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connectivity failure"));

        NewsletterSchedulerWorkerService sut = new(
            schedule.Object,
            Mock.Of<IBackgroundJobClient>(),
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            Options.Create(new NewsletterOptions()));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RunAsync());
    }
}
