using Hermes.Application.DTOs.Email;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Options.External;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Newsletter;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
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
    private static void SetupAdvanceDigestSlot(Mock<INewsletterSubscriptionRepository> newsStore)
    {
        newsStore.Setup(s => s.AdvanceNextDigestSlotAsync(
                It.IsAny<NewsletterId>(),
                It.IsAny<UserId>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
    }

    /// <summary>
    /// Helper method to create a default newsletter subscription store mock.
    /// </summary>
    private static INewsletterSubscriptionRepository CreateDefaultNewsStore()
    {
        Mock<INewsletterSubscriptionRepository> mock = new();
        mock.Setup(s => s.AdvanceNextDigestSlotAsync(
                It.IsAny<NewsletterId>(),
                It.IsAny<UserId>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        return mock.Object;
    }

    /// <summary>
    /// Helper method to initialize a system under test (SUT) with optional mocks.
    /// </summary>
    private static NewsletterDigestService CreateSut(
        IUserRepository? users = null,
        INewsletterSubscriptionRepository? news = null,
        INotificationLogRepository? notificationLogs = null,
        IArticleFetchingService? articleFetchingService = null,
        IEmailProvider? emailSender = null,
        INewsletterHtmlService? newsletterRenderer = null,
        IOptions<NewsletterOptions>? newsletterOptions = null,
        ILogger<NewsletterDigestService>? logger = null)
    {
        if (newsletterRenderer is null)
        {
            Mock<INewsletterHtmlService> rendererMock = new();
            rendererMock
                .Setup(r => r.RenderNewsletterAsync(It.IsAny<NewsletterRenderRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("<html>test-newsletter</html>");
            newsletterRenderer = rendererMock.Object;
        }

        return new NewsletterDigestService(
            users ?? Mock.Of<IUserRepository>(),
            news ?? CreateDefaultNewsStore(),
            notificationLogs ?? Mock.Of<INotificationLogRepository>(),
            articleFetchingService ?? Mock.Of<IArticleFetchingService>(),
            emailSender ?? Mock.Of<IEmailProvider>(),
            newsletterRenderer,
            newsletterOptions ?? Options.Create(new NewsletterOptions()),
            TimeProvider.System,
            logger ?? Mock.Of<ILogger<NewsletterDigestService>>());
    }

    private static NewsletterSubscription CreateNews(NewsletterId id, UserId userId, string[]? keywords = null, bool isEnabled = true)
    {
        var news = NewsletterSubscription.CreateForUser(userId);
        news.UpdateFilters(keywords, null, null, null);
        if (!isEnabled) news.Disable();
        news.SetId(id);
        return news;
    }

    // [B]OUNDARY: Rejects non-positive user ID or news ID input parameters
    /// <summary>
    /// Verifies that SendAsync throws an ArgumentOutOfRangeException for non-positive IDs.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-5, 10)]
    public async Task SendAsync_Should_RejectNonPositiveUserOrNewsIdentifiers(int userId, int newsId)
    {
        // Arrange
        NewsletterDigestService sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.SendAsync(new UserId(userId), new NewsletterId(newsId), DateTime.UtcNow));
    }


    // [B]OUNDARY: Aborts early when a duplicate notification was already sent in the UTC minute window
    /// <summary>
    /// Verifies that SendAsync returns early and does not load user/subscription if a duplicate log is found in the current minute window.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_NotLoadUserOrNews_WhenDuplicateAlreadySentInWindow()
    {
        // Arrange
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<UserId>(),
                It.IsAny<NewsletterId>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock<IUserRepository> users = new();
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);

        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object);

        // Act
        await sut.SendAsync(new UserId(5), new NewsletterId(10), new DateTime(2026, 6, 15, 14, 30, 22, DateTimeKind.Utc));

        // Assert
        users.Verify(store => store.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Never);
        newsPort.Verify(store => store.GetNewsByIdAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<CancellationToken>()), Times.Never);
        newsPort.Verify(
            store => store.AdvanceNextDigestSlotAsync(
                new NewsletterId(10),
                new UserId(5),
                It.IsAny<TimeZoneInfo>(),
                It.Is<DateTime>(dt => dt.Kind == DateTimeKind.Utc),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // [B]OUNDARY: Advances digest slot without dispatching email when article fetch yields zero results
    [Fact]
    public async Task SendAsync_Should_AdvanceSlot_ButNotSendEmail_WhenNoArticlesFound()
    {
        // Arrange
        Mock<IArticleFetchingService> articleFetchingService = new();
        articleFetchingService.Setup(p => p.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NewsArticle>());

        Mock<IUserRepository> users = new();
        users.Setup(u => u.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(5), Name = "Alice", Email = Email.Parse("alice@test.com") });

        Mock<INewsletterSubscriptionRepository> newsStore = new();
        newsStore.Setup(s => s.GetNewsByIdAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNews(new NewsletterId(10), new UserId(5), ["test"], true));
        SetupAdvanceDigestSlot(newsStore);

        Mock<IEmailProvider> emailSender = new();
        Mock<INotificationLogRepository> logs = new();

        NewsletterDigestService sut = CreateSut(users.Object, newsStore.Object, logs.Object, articleFetchingService.Object, emailSender.Object);

        // Act
        await sut.SendAsync(new UserId(5), new NewsletterId(10), new DateTime(2026, 6, 15, 14, 30, 22, DateTimeKind.Utc));

        // Assert
        newsStore.Verify(s => s.AdvanceNextDigestSlotAsync(new NewsletterId(10), new UserId(5), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        emailSender.Verify(e => e.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // [R]IGHT: Normalizes digest slot UTC timestamp to top of minute for duplicate check window
    /// <summary>
    /// Verifies that the duplicate check uses a normalized minute slice.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_CheckDuplicateWindow_WithNormalizedUtcMinuteSlice()
    {
        // Arrange
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<UserId>(),
                It.IsAny<NewsletterId>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        NewsletterDigestService sut = CreateSut(notificationLogs: logs.Object);
        DateTime digestUtc = new(2026, 3, 20, 9, 45, 59, DateTimeKind.Utc);
        DateTime expectedStart = new(2026, 3, 20, 9, 45, 0, DateTimeKind.Utc);
        DateTime expectedEnd = expectedStart.AddMinutes(1);

        // Act
        await sut.SendAsync(new UserId(1), new NewsletterId(2), digestUtc);

        // Assert
        logs.Verify(
            s => s.ExistsSentNotificationInWindowAsync(new UserId(1), new NewsletterId(2), expectedStart, expectedEnd, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // [B]OUNDARY: Aborts execution silently when requested user record is missing
    /// <summary>
    /// Verifies that SendAsync aborts silently if the target user is not found.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_AbortSilently_WhenUserMissing()
    {
        // Arrange
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<UserId>(),
                It.IsAny<NewsletterId>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(new UserId(7), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        Mock<IArticleFetchingService> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, notificationLogs: logs.Object, articleFetchingService: articles.Object);

        // Act
        await sut.SendAsync(new UserId(7), new NewsletterId(99), DateTime.UtcNow);

        // Assert
        articles.Verify(
            articleProvider => articleProvider.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // [B]OUNDARY: Aborts execution silently when newsletter subscription profile does not exist
    /// <summary>
    /// Verifies that SendAsync aborts silently if the subscription configuration is not found.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_AbortSilently_WhenNewsProfileMissing()
    {
        // Arrange
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<UserId>(),
                It.IsAny<NewsletterId>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = Email.Parse("a@b.c"), Name = "Anna" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(new UserId(1), new NewsletterId(88), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewsletterSubscription?)null);

        Mock<IArticleFetchingService> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object);

        // Act
        await sut.SendAsync(new UserId(1), new NewsletterId(88), DateTime.UtcNow);

        // Assert
        articles.Verify(
            articleProvider => articleProvider.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // [B]OUNDARY: Advances slot without sending email when newsletter subscription is disabled
    /// <summary>
    /// Verifies that SendAsync skips article query logic and advances scheduling slots if the subscription is disabled.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_SkipSend_AndAdvanceSlot_WhenNewsDisabled()
    {
        // Arrange
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<UserId>(),
                It.IsAny<NewsletterId>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = Email.Parse("off@b.c"), Name = "Off" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(new UserId(1), new NewsletterId(42), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNews(new NewsletterId(42), new UserId(1), ["x"], false));

        Mock<IArticleFetchingService> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object);

        // Act
        await sut.SendAsync(new UserId(1), new NewsletterId(42), DateTime.UtcNow);

        // Assert
        articles.Verify(
            articleProvider => articleProvider.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()),
            Times.Never);
        newsPort.Verify(
            store => store.AdvanceNextDigestSlotAsync(
                new NewsletterId(42),
                new UserId(1),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }


    // [R]IGHT: Standard success pipeline renders template, sends email, writes audit log, and advances slot
    /// <summary>
    /// Verifies that SendAsync sends a rendered digest, saves a successful log, and advances scheduling slots under normal operation.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_SendMail_WriteSentLog_WhenPipelineSucceeds()
    {
        // Arrange
        NewsletterSubscription? capturedQuery = null;
        NotificationLog? capturedLog = null;

        NewsletterSubscription news = CreateNews(new NewsletterId(12), new UserId(2), ["Berlin"]);

        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<UserId>(),
                It.IsAny<NewsletterId>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(new UserId(2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(2), Email = Email.Parse("digest@test.example"), Name = "Dieter" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(new UserId(2), new NewsletterId(12), It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<IArticleFetchingService> articles = new();
        articles.Setup(articleProvider => articleProvider.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .Callback<NewsletterSubscription, CancellationToken>((s, _) => capturedQuery = s)
            .ReturnsAsync([new NewsArticle("1", "A", "desc", "http://a", null, null)]);

        Mock<IEmailProvider> email = new();
        email.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        logs.Setup(s => s.SetNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationLog, CancellationToken>((log, _) => capturedLog = log)
            .Returns(ValueTask.CompletedTask);

        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object, email.Object);

        // Act
        await sut.SendAsync(new UserId(2), new NewsletterId(12), new DateTime(2026, 8, 1, 11, 0, 0, DateTimeKind.Utc));

        // Assert
        Assert.NotNull(capturedQuery);

        Assert.NotNull(capturedLog);
        Assert.Equal(new UserId(2), capturedLog!.UserId);
        Assert.Equal(new NewsletterId(12), capturedLog.NewsId);
        Assert.Equal(NotificationStatus.Sent, capturedLog.Status);
        Assert.Equal(DeliveryChannel.Email, capturedLog.Channel);

        email.Verify(
            emailSender => emailSender.SendAsync(
                It.Is<EmailMessageDto>(emailMessage =>
                    emailMessage.To.Address == "digest@test.example"
                    && emailMessage.Subject.Contains("#12", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // [E]RROR: Logs failed delivery attempt and rethrows exception when SMTP provider fails
    /// <summary>
    /// Verifies that SendAsync logs delivery failures and propagates exceptions to Hangfire when the email sender fails.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_WriteFailedLog_AndPropagate_WhenSmtpFails()
    {
        // Arrange
        NewsletterSubscription news = CreateNews(new NewsletterId(1), new UserId(1), ["test"]);

        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<UserId>(),
                It.IsAny<NewsletterId>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = Email.Parse("fail@test.example"), Name = "F" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(new UserId(1), new NewsletterId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<IArticleFetchingService> articles = new();
        articles.Setup(articleProvider => articleProvider.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new NewsArticle("2", "X", "desc", "http://x", null, null)]);

        Mock<IEmailProvider> email = new();
        email.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        NotificationLog? capturedFailed = null;
        logs.Setup(s => s.SetNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationLog, CancellationToken>((log, _) => capturedFailed = log)
            .Returns(ValueTask.CompletedTask);

        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object, email.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SendAsync(new UserId(1), new NewsletterId(1), DateTime.UtcNow));

        Assert.NotNull(capturedFailed);
        Assert.Equal(NotificationStatus.Failed, capturedFailed!.Status);
        Assert.Equal("SMTP unavailable", capturedFailed.ErrorMessage);
    }
}




