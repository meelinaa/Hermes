using Hermes.Application.DTOs.Email;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services;
using Hermes.Notifications.Receiving.Options;
using Hermes.Worker.Services.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Worker.Scheduling;

public sealed class NewsletterSchedulerTests
{
    private static EmailOptions CreateEmailOptions() =>
        new(
            Host: "localhost",
            Port: 1025,
            EnableSsl: false,
            Username: null,
            Password: null,
            DefaultFromAddress: "from@test.local",
            DefaultFromName: "Hermes",
            DefaultReplyToAddress: "reply@test.local",
            DefaultReplyToName: "Reply",
            XMailer: "Hermes.UnitTests");

    [Fact]
    public async Task RunAsync_Should_QueryDueProfilesOnce_AndSkipMail_WhenNothingDue_AndMailHogDisabled()
    {
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(scheduleService => scheduleService.GetDueItemsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(int NewsId, int UserId)>());

        Mock<IEmailProvider> emailSender = new();

        NewsletterSchedulerWorkerService sut = new(
            schedule.Object,
            new Mock<global::Hangfire.IBackgroundJobClient>().Object,
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            emailSender.Object,
            CreateEmailOptions(),
            Options.Create(new MailHogOptions { SendSchedulerTestMailEachMinute = false }),
            Options.Create(new NewsletterOptions()));
        await sut.RunAsync();
        schedule.Verify(
            scheduleService => scheduleService.GetDueItemsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        emailSender.Verify(
            sender => sender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            Mock.Of<IEmailProvider>(),
            CreateEmailOptions(),
            Options.Create(new MailHogOptions()),
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
        IReadOnlyList<(int NewsId, int UserId)> dueItems = [(10, 1), (11, 2)];
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(s => s.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dueItems);

        Mock<global::Hangfire.IBackgroundJobClient> jobClient = new();

        NewsletterSchedulerWorkerService sut = new(
            schedule.Object,
            jobClient.Object,
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            Mock.Of<IEmailProvider>(),
            CreateEmailOptions(),
            Options.Create(new MailHogOptions { SendSchedulerTestMailEachMinute = false }),
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
    [Fact]
    public async Task RunAsync_Should_SendTestMail_WhenMailHogIsEnabled()
    {
        // Arrange
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(s => s.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(int, int)>());

        Mock<IEmailProvider> emailSender = new();

        NewsletterSchedulerWorkerService sut = new(
            schedule.Object,
            Mock.Of<global::Hangfire.IBackgroundJobClient>(),
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            emailSender.Object,
            CreateEmailOptions(),
            Options.Create(new MailHogOptions { SendSchedulerTestMailEachMinute = true }),
            Options.Create(new NewsletterOptions()));

        // Act
        await sut.RunAsync();

        // Assert
        emailSender.Verify(
            x => x.SendAsync(It.Is<EmailMessageDto>(m => m.Subject.Contains("[Hermes/MailHog] Scheduler-Test")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_Should_CatchOperationCanceledException_WhenMailHogIsEnabled()
    {
        // Arrange
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(s => s.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(int, int)>());

        Mock<IEmailProvider> emailSender = new();
        emailSender.Setup(x => x.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        NewsletterSchedulerWorkerService sut = new(
            schedule.Object,
            Mock.Of<global::Hangfire.IBackgroundJobClient>(),
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            emailSender.Object,
            CreateEmailOptions(),
            Options.Create(new MailHogOptions { SendSchedulerTestMailEachMinute = true }),
            Options.Create(new NewsletterOptions()));

        // Act
        Exception? exception = await Record.ExceptionAsync(() => sut.RunAsync(new CancellationToken()));

        // Assert
        Assert.Null(exception); // Must not throw
    }

    [Fact]
    public async Task RunAsync_Should_CatchGenericException_WhenMailHogIsEnabled()
    {
        // Arrange
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(s => s.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(int, int)>());

        Mock<IEmailProvider> emailSender = new();
        emailSender.Setup(x => x.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated error"));

        NewsletterSchedulerWorkerService sut = new(
            schedule.Object,
            Mock.Of<global::Hangfire.IBackgroundJobClient>(),
            NullLogger<NewsletterSchedulerWorkerService>.Instance,
            emailSender.Object,
            CreateEmailOptions(),
            Options.Create(new MailHogOptions { SendSchedulerTestMailEachMinute = true }),
            Options.Create(new NewsletterOptions()));

        // Act
        Exception? exception = await Record.ExceptionAsync(() => sut.RunAsync());

        // Assert
        Assert.Null(exception); // Must not throw
    }
}
