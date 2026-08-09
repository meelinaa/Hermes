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
                It.IsAny<int>(),
                It.IsAny<int>(),
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
                It.IsAny<int>(),
                It.IsAny<int>(),
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
        INewsArticleProvider? newsProvider = null,
        IEmailProvider? emailSender = null,
        INewsletterHtmlService? newsletterRenderer = null,
        IOptions<NewsDataIoOptions>? newsOptions = null,
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
            newsProvider ?? Mock.Of<INewsArticleProvider>(),
            emailSender ?? Mock.Of<IEmailProvider>(),
            newsletterRenderer,
            newsOptions ?? Options.Create(new NewsDataIoOptions { Key = "integration-test-api-key" }),
            newsletterOptions ?? Options.Create(new NewsletterOptions()),
            logger ?? Mock.Of<ILogger<NewsletterDigestService>>());
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
            sut.SendAsync(userId, newsId, DateTime.UtcNow));
    }

    // [B]OUNDARY: Throws when NewsDataIo API key configuration is missing or blank
    /// <summary>
    /// Verifies that SendAsync throws an InvalidOperationException if the API Key is empty or whitespace.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ThrowInvalidOperation_WhenApiKeyMissingOrWhitespaceOnly()
    {
        // Arrange
        NewsletterDigestService sutEmpty = CreateSut(newsOptions: Options.Create(new NewsDataIoOptions { Key = "" }));
        NewsletterDigestService sutWs = CreateSut(newsOptions: Options.Create(new NewsDataIoOptions { Key = "   " }));

        // Act & Assert
        InvalidOperationException ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sutEmpty.SendAsync(1, 1, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("Configure NewsDataIo:Key.", ex1.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sutWs.SendAsync(1, 1, DateTime.UtcNow));
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
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock<IUserRepository> users = new();
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);

        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object);

        // Act
        await sut.SendAsync(5, 10, new DateTime(2026, 6, 15, 14, 30, 22, DateTimeKind.Utc));

        // Assert
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

    // [B]OUNDARY: Advances digest slot without dispatching email when article fetch yields zero results
    [Fact]
    public async Task SendAsync_Should_AdvanceSlot_ButNotSendEmail_WhenNoArticlesFound()
    {
        // Arrange
        Mock<INewsArticleProvider> newsProvider = new();
        newsProvider.Setup(p => p.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NewsArticle>());

        Mock<IUserRepository> users = new();
        users.Setup(u => u.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, Name = "Alice", Email = "alice@test.com" });

        Mock<INewsletterSubscriptionRepository> newsStore = new();
        newsStore.Setup(s => s.GetNewsByIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NewsletterSubscription { Id = 10, UserId = 5, IsEnabled = true, Keywords = ["test"] });
        SetupAdvanceDigestSlot(newsStore);

        Mock<IEmailProvider> emailSender = new();
        Mock<INotificationLogRepository> logs = new();

        NewsletterDigestService sut = CreateSut(users.Object, newsStore.Object, logs.Object, newsProvider.Object, emailSender.Object);

        // Act
        await sut.SendAsync(5, 10, new DateTime(2026, 6, 15, 14, 30, 22, DateTimeKind.Utc));

        // Assert
        newsStore.Verify(s => s.AdvanceNextDigestSlotAsync(10, 5, It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
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

        // Act
        await sut.SendAsync(1, 2, digestUtc);

        // Assert
        logs.Verify(
            s => s.ExistsSentNotificationInWindowAsync(1, 2, expectedStart, expectedEnd, It.IsAny<CancellationToken>()),
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
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, notificationLogs: logs.Object, newsProvider: articles.Object);

        // Act
        await sut.SendAsync(7, 99, DateTime.UtcNow);

        // Assert
        articles.Verify(
            articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // [B]OUNDARY: Aborts execution silently when target user email is blank
    /// <summary>
    /// Verifies that SendAsync aborts silently if the target user email address is empty.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_AbortSilently_WhenUserHasNoDeliverableEmail()
    {
        // Arrange
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "   ", Name = "X" });

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, notificationLogs: logs.Object, newsProvider: articles.Object);

        // Act
        await sut.SendAsync(1, 2, DateTime.UtcNow);

        // Assert
        articles.Verify(
            articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()),
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
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "a@b.c", Name = "Anna" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(1, 88, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewsletterSubscription?)null);

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object);

        // Act
        await sut.SendAsync(1, 88, DateTime.UtcNow);

        // Assert
        articles.Verify(
            articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()),
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
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "off@b.c", Name = "Off" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(1, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NewsletterSubscription { Id = 42, UserId = 1, Keywords = ["x"], IsEnabled = false });

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object);

        // Act
        await sut.SendAsync(1, 42, DateTime.UtcNow);

        // Assert
        articles.Verify(
            articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()),
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

    // [B]OUNDARY: Skips API fetch when filter criteria result in an empty query object
    /// <summary>
    /// Verifies that SendAsync does not call the news API provider if the subscription produces an empty query payload.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_NotCallNewsApi_WhenFiltersProduceNoQuery()
    {
        // Arrange
        NewsletterSubscription news = new()
        {
            Id = 3,
            UserId = 1,
            Keywords = ["   "],
            Countries = [],
            Languages = [],
            Category = [],
        };
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "user@test.example", Name = "U" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object);

        // Act
        await sut.SendAsync(1, 3, DateTime.UtcNow);

        // Assert
        articles.Verify(
            articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // [R]IGHT: Standard success pipeline renders template, sends email, writes audit log, and advances slot
    /// <summary>
    /// Verifies that SendAsync sends a rendered digest, saves a successful log, and advances scheduling slots under normal operation.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_SendMail_WriteSentLog_WhenPipelineSucceeds()
    {
        // Arrange
        NewsArticleQueryDto? capturedQuery = null;
        NotificationLog? capturedLog = null;

        NewsletterSubscription news = new()
        {
            Id = 12,
            UserId = 2,
            Keywords = ["Berlin"],
        };

        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 2, Email = "digest@test.example", Name = "Dieter" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(2, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<INewsArticleProvider> articles = new();
        articles.Setup(articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<NewsArticleQueryDto, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync([new NewsArticle("1", "A", "desc", "http://a", null, null)]);

        Mock<IEmailProvider> email = new();
        email.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        logs.Setup(s => s.SetNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationLog, CancellationToken>((log, _) => capturedLog = log)
            .Returns(ValueTask.CompletedTask);

        NewsletterDigestService sut = CreateSut(users.Object, newsPort.Object, logs.Object, articles.Object, email.Object);

        // Act
        await sut.SendAsync(2, 12, new DateTime(2026, 8, 1, 11, 0, 0, DateTimeKind.Utc));

        // Assert
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
        NewsletterSubscription news = new() { Id = 1, UserId = 1, Keywords = ["test"] };

        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "fail@test.example", Name = "F" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        SetupAdvanceDigestSlot(newsPort);
        newsPort.Setup(store => store.GetNewsByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<INewsArticleProvider> articles = new();
        articles.Setup(articleProvider => articleProvider.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()))
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
            sut.SendAsync(1, 1, DateTime.UtcNow));

        Assert.NotNull(capturedFailed);
        Assert.Equal(NotificationStatus.Failed, capturedFailed!.Status);
        Assert.Equal("SMTP unavailable", capturedFailed.ErrorMessage);
    }
}
