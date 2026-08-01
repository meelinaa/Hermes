using Hermes.Application.DTOs.Email;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services;
using Hermes.Notifications.Receiving.Models;
using Hermes.Worker.Scheduling;
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
}
