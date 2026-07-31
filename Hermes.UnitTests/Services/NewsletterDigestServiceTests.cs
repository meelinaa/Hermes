using Hermes.Application.DTOs.Email;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Unit tests for <see cref="NewsletterDigestService"/>.
/// </summary>
public sealed class NewsletterDigestServiceTests
{
    /// <summary>
    /// Set up a mock helper to simulate successful NextDigestSlotUtc advancement.
    /// </summary>
    private static void SetupAdvanceDigestSlot(Mock<INewsletterSubscriptionStore> newsStore)
    {
        newsStore.Setup(s => s.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// Helper method to create a default newsletter subscription store mock.
    /// </summary>
    private static INewsletterSubscriptionStore CreateDefaultNewsStore()
    {
        Mock<INewsletterSubscriptionStore> mock = new();
        mock.Setup(s => s.AdvanceNextDigestSlotAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock.Object;
    }

    /// <summary>
    /// Helper method to initialize a system under test (SUT) with optional mocks.
    /// </summary>
    private static NewsletterDigestService CreateSut(
        IUserStore? users = null,
        INewsletterSubscriptionStore? news = null,
        INotificationLogStore? notificationLogs = null,
        INewsArticleProvider? newsProvider = null,
        IEmailSender? emailSender = null,
        INewsletterRenderer? newsletterRenderer = null,
        IOptions<NewsDataIoOptions>? newsOptions = null,
        IOptions<NewsletterOptions>? newsletterOptions = null,
        ILogger<NewsletterDigestService>? logger = null)
    {
        if (newsletterRenderer is null)
        {
            Mock<INewsletterRenderer> rendererMock = new();
            rendererMock
                .Setup(r => r.RenderNewsletterAsync(It.IsAny<NewsletterRenderRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("<html>test-newsletter</html>");
            newsletterRenderer = rendererMock.Object;
        }

        return new NewsletterDigestService(
            users ?? Mock.Of<IUserStore>(),
            news ?? CreateDefaultNewsStore(),
            notificationLogs ?? Mock.Of<INotificationLogStore>(),
            newsProvider ?? Mock.Of<INewsArticleProvider>(),
            emailSender ?? Mock.Of<IEmailSender>(),
            newsletterRenderer,
            newsOptions ?? Options.Create(new NewsDataIoOptions { Key = "integration-test-api-key" }),
            newsletterOptions ?? Options.Create(new NewsletterOptions()),
            logger ?? Mock.Of<ILogger<NewsletterDigestService>>());
    }

    /// <summary>
    /// Verifies that SendAsync throws an ArgumentOutOfRangeException for non-positive IDs.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-5, 10)]
    public async Task SendAsync_Should_RejectNonPositiveUserOrNewsIdentifiers(int userId, int newsId)
    {
        NewsletterDigestService sut = CreateSut();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.SendAsync(userId, newsId, DateTime.UtcNow));
    }

    /// <summary>
    /// Verifies that SendAsync throws an InvalidOperationException if the API Key is empty or whitespace.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ThrowInvalidOperation_WhenApiKeyMissingOrWhitespaceOnly()
    {
        NewsletterDigestService sutEmpty = CreateSut(newsOptions: Options.Create(new NewsDataIoOptions { Key = "" }));
        NewsletterDigestService sutWs = CreateSut(newsOptions: Options.Create(new NewsDataIoOptions { Key = "   " }));
        InvalidOperationException ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sutEmpty.SendAsync(1, 1, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("Configure NewsDataIo:Key.", ex1.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sutWs.SendAsync(1, 1, DateTime.UtcNow));
    }

    /// <summary>
    /// Verifies that SendAsync returns early and does not load user/subscription if a duplicate log is found in the current minute window.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_NotLoadUserOrNews_WhenDuplicateAlreadySentInWindow()
    {
        Mock<INotificationLogStore> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock<IUserStore> users = new();
        Mock<INewsletterSubscriptionStore> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);

        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object);
        await sut.SendAsync(5, 10, new DateTime(2026, 6, 15, 14, 30, 22, DateTimeKind.Utc));
        users.Verify(store => store.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        newsPort.Verify(store => store.GetNewsByIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        newsPort.Verify(
            store => store.AdvanceNextDigestSlotAsync(
                10,
                5,
                It.IsAny<TimeZoneInfo>(),
                It.Is<DateTime>(dt => dt.Kind == DateTimeKind.Utc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that the duplicate check uses a normalized minute slice.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_CheckDuplicateWindow_WithNormalizedUtcMinuteSlice()
    {
        Mock<INotificationLogStore> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        NewsletterDigestService sut = CreateSut(notificationLogs: logs.Object);
        DateTime digestUtc = new(2026, 3, 20, 9, 45, 59, DateTimeKind.Utc);
        DateTime expectedStart = new(2026, 3, 20, 9, 45, 0, DateTimeKind.Utc);
        DateTime expectedEnd = expectedStart.AddMinutes(1);
        await sut.SendAsync(1, 2, digestUtc);
        logs.Verify(
            s => s.ExistsSentNotificationInWindowAsync(1, 2, expectedStart, expectedEnd, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that SendAsync aborts silently if the target user is not found.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_AbortSilently_WhenUserMissing()
    {
        Mock<INotificationLogStore> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IUserStore> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, notificationLogs: logs.Object, newsProvider: articles.Object);

        await sut.SendAsync(7, 99, DateTime.UtcNow);

        articles.Verify(
            articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that SendAsync aborts silently if the target user email address is empty.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_AbortSilently_WhenUserHasNoDeliverableEmail()
    {
        Mock<INotificationLogStore> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IUserStore> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "   ", Name = "X" });

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, notificationLogs: logs.Object, newsProvider: articles.Object);

        await sut.SendAsync(1, 2, DateTime.UtcNow);

        articles.Verify(
            articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that SendAsync aborts silently if the subscription configuration is not found.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_AbortSilently_WhenNewsProfileMissing()
    {
        Mock<INotificationLogStore> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IUserStore> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "a@b.c", Name = "Anna" });
        Mock<INewsletterSubscriptionStore> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(1, 88, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewsletterSubscription?)null);

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object);

        await sut.SendAsync(1, 88, DateTime.UtcNow);

        articles.Verify(
            articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that SendAsync skips article query logic and advances scheduling slots if the subscription is disabled.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_SkipSend_AndAdvanceSlot_WhenNewsDisabled()
    {
        Mock<INotificationLogStore> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IUserStore> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "off@b.c", Name = "Off" });
        Mock<INewsletterSubscriptionStore> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(1, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NewsletterSubscription { Id = 42, UserId = 1, Keywords = ["x"], IsEnabled = false });

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object);

        await sut.SendAsync(1, 42, DateTime.UtcNow);

        articles.Verify(
            articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        newsPort.Verify(
            store => store.AdvanceNextDigestSlotAsync(
                42,
                1,
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that SendAsync does not call the news API provider if the subscription produces an empty query payload.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_NotCallNewsApi_WhenFiltersProduceNoQuery()
    {
        NewsletterSubscription news = new()
        {
            Id = 3,
            UserId = 1,
            Keywords = ["   "],
            Countries = [],
            Languages = [],
            Category = [],
        };
        Mock<INotificationLogStore> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IUserStore> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "user@test.example", Name = "U" });
        Mock<INewsletterSubscriptionStore> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object);
        await sut.SendAsync(1, 3, DateTime.UtcNow);
        articles.Verify(
            articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Verifies that SendAsync sends a rendered digest, saves a successful log, and advances scheduling slots under normal operation.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_SendMail_WriteSentLog_WhenPipelineSucceeds()
    {
        NewsArticleQuery? capturedQuery = null;
        NotificationLog? capturedLog = null;

        NewsletterSubscription news = new()
        {
            Id = 12,
            UserId = 2,
            Keywords = ["Berlin"],
        };

        Mock<INotificationLogStore> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IUserStore> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 2, Email = "digest@test.example", Name = "Dieter" });
        Mock<INewsletterSubscriptionStore> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(2, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<INewsArticleProvider> articles = new();
        articles.Setup(articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()))
            .Callback<NewsArticleQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync([]);

        Mock<IEmailSender> email = new();
        email.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        logs.Setup(s => s.SetNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationLog, CancellationToken>((log, _) => capturedLog = log)
            .Returns(Task.CompletedTask);

        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object, email.Object);
        await sut.SendAsync(2, 12, new DateTime(2026, 8, 1, 11, 0, 0, DateTimeKind.Utc));
        Assert.NotNull(capturedQuery);
        Assert.Equal("integration-test-api-key", capturedQuery!.ApiKey);
        Assert.Equal("Berlin", capturedQuery.KeywordsQuery);

        Assert.NotNull(capturedLog);
        Assert.Equal(2, capturedLog!.UserId);
        Assert.Equal(12, capturedLog.NewsId);
        Assert.Equal(NotificationStatus.Sent, capturedLog.Status);
        Assert.Equal(DeliveryChannel.Email, capturedLog.Channel);

        email.Verify(
            emailSender => emailSender.SendAsync(
                It.Is<EmailMessage>(emailMessage =>
                    emailMessage.To.Address == "digest@test.example"
                    && emailMessage.Subject.Contains("#12", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that SendAsync logs delivery failures and propagates exceptions to Hangfire when the email sender fails.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_WriteFailedLog_AndPropagate_WhenSmtpFails()
    {
        NewsletterSubscription news = new() { Id = 1, UserId = 1, Keywords = ["test"] };

        Mock<INotificationLogStore> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IUserStore> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "fail@test.example", Name = "F" });
        Mock<INewsletterSubscriptionStore> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<INewsArticleProvider> articles = new();
        articles.Setup(articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Mock<IEmailSender> email = new();
        email.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        NotificationLog? capturedFailed = null;
        logs.Setup(s => s.SetNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationLog, CancellationToken>((log, _) => capturedFailed = log)
            .Returns(Task.CompletedTask);

        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object, email.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SendAsync(1, 1, DateTime.UtcNow));

        Assert.NotNull(capturedFailed);
        Assert.Equal(NotificationStatus.Failed, capturedFailed!.Status);
        Assert.Equal("SMTP unavailable", capturedFailed.ErrorMessage);
    }
}
