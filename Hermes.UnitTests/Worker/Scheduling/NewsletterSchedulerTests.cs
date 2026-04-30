using Hermes.Application.Models.Email;
using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Notifications.Receiving.Models;
using Hermes.Worker.Scheduling;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Worker.Scheduling;

/// <summary>
/// Specifications for the worker scheduler loop: query due newsletter profiles once per run; optional MailHog test mail path stays isolated.
/// </summary>
public sealed class NewsletterSchedulerTests
{
    private static EmailSettings CreateEmailSettings() =>
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

    /// <summary>
    /// Each scheduler tick asks schedule service for due items once; with nothing due and MailHog diagnostic disabled, no mail is sent.
    /// </summary>
    [Fact]
    public async Task RunAsync_Should_QueryDueProfilesOnce_AndSkipMail_WhenNothingDue_AndMailHogDisabled()
    {
        // Arrange
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(s => s.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(int NewsId, int UserId)>());

        Mock<IEmailSender> emailSender = new();

        NewsletterScheduler sut = new(
            schedule.Object,
            NullLogger<NewsletterScheduler>.Instance,
            emailSender.Object,
            CreateEmailSettings(),
            Options.Create(new MailHogSettings { SendSchedulerTestMailEachMinute = false }));

        // Act
        await sut.RunAsync();

        // Assert
        schedule.Verify(
            x => x.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        emailSender.Verify(
            x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Same cancellation token instance passed to <see cref="NewsletterScheduler.RunAsync(System.Threading.CancellationToken)"/> must reach <see cref="INewsletterScheduleService.GetDueItemsAsync"/>.
    /// </summary>
    [Fact]
    public async Task RunAsync_Should_ForwardSameCancellationToken_ToScheduleService()
    {
        // Arrange
        CancellationToken? captured = null;
        Mock<INewsletterScheduleService> schedule = new();
        schedule.Setup(s => s.GetDueItemsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((_, ct) => captured = ct)
            .ReturnsAsync([]);

        NewsletterScheduler sut = new(
            schedule.Object,
            NullLogger<NewsletterScheduler>.Instance,
            Mock.Of<IEmailSender>(),
            CreateEmailSettings(),
            Options.Create(new MailHogSettings()));

        using CancellationTokenSource cts = new();

        // Act
        await sut.RunAsync(cts.Token);

        // Assert
        Assert.True(captured.HasValue);
        Assert.Equal(cts.Token, captured.Value);
    }
}
