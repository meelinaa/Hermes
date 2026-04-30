using Hermes.Application.Models.Email;
using Hermes.Application.Models.News;
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
/// Specifications for newsletter digest sending: validate ids and API key; dedupe window before expensive work;
/// graceful skips; on SMTP failure persist Failed log and propagate the exception.
/// </summary>
public sealed class NewsletterDigestServiceTests
{
    private static NewsletterDigestService CreateSut(
        IHermesDataStore store,
        INewsArticleProvider? newsProvider = null,
        IEmailSender? emailSender = null,
        IOptions<NewsDataIoOptions>? newsOptions = null,
        ILogger<NewsletterDigestService>? logger = null)
    {
        return new NewsletterDigestService(
            store,
            newsProvider ?? Mock.Of<INewsArticleProvider>(),
            emailSender ?? Mock.Of<IEmailSender>(),
            newsOptions ?? Options.Create(new NewsDataIoOptions { ApiKey = "integration-test-api-key" }),
            logger ?? Mock.Of<ILogger<NewsletterDigestService>>());
    }

    /// <summary>
    /// Both user id and news id must be positive before any I/O.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-5, 10)]
    public async Task SendAsync_Should_RejectNonPositiveUserOrNewsIdentifiers(int userId, int newsId)
    {
        NewsletterDigestService sut = CreateSut(Mock.Of<IHermesDataStore>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.SendAsync(userId, newsId, DateTime.UtcNow));
    }

    /// <summary>
    /// Missing or whitespace-only NewsData.io API key must fail fast with a fixed configuration message.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ThrowInvalidOperation_WhenApiKeyMissingOrWhitespaceOnly()
    {
        // Arrange
        NewsletterDigestService sutEmpty = CreateSut(Mock.Of<IHermesDataStore>(), newsOptions: Options.Create(new NewsDataIoOptions { ApiKey = "" }));
        NewsletterDigestService sutWs = CreateSut(Mock.Of<IHermesDataStore>(), newsOptions: Options.Create(new NewsDataIoOptions { ApiKey = "   " }));

        // Act / Assert
        InvalidOperationException ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sutEmpty.SendAsync(1, 1, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("Configure NewsDataIo:ApiKey.", ex1.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sutWs.SendAsync(1, 1, DateTime.UtcNow));
    }

    /// <summary>
    /// If a notification was already sent in the duplicate-detection window for this user/news slice, skip loading user/news (cheap guard).
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_NotLoadUserOrNews_WhenDuplicateAlreadySentInWindow()
    {
        // Arrange — duplicate check returns true immediately
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        NewsletterDigestService sut = CreateSut(store.Object);

        // Act
        await sut.SendAsync(5, 10, new DateTime(2026, 6, 15, 14, 30, 22, DateTimeKind.Utc));

        // Assert — heavier store queries never run
        store.Verify(s => s.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(s => s.GetNewsByIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Duplicate window must use UTC minute normalization: [minute floor, minute floor + 1 minute) half-open interval.
    /// </summary>
    /// <remarks>
    /// Seconds within the same UTC minute must map to the same window start/end passed to the store.
    /// </remarks>
    [Fact]
    public async Task SendAsync_Should_CheckDuplicateWindow_WithNormalizedUtcMinuteSlice()
    {
        // Arrange
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        NewsletterDigestService sut = CreateSut(store.Object);
        DateTime digestUtc = new(2026, 3, 20, 9, 45, 59, DateTimeKind.Utc);
        DateTime expectedStart = new(2026, 3, 20, 9, 45, 0, DateTimeKind.Utc);
        DateTime expectedEnd = expectedStart.AddMinutes(1);

        // Act
        await sut.SendAsync(1, 2, digestUtc);

        // Assert
        store.Verify(
            s => s.ExistsSentNotificationInWindowAsync(1, 2, expectedStart, expectedEnd, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Missing user entity ends the pipeline silently (no external API call).
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_AbortSilently_WhenUserMissing()
    {
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        store.Setup(s => s.GetUserEntityByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(store.Object, articles.Object);

        await sut.SendAsync(7, 99, DateTime.UtcNow);

        articles.Verify(
            a => a.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// User with no deliverable email (blank/whitespace) cannot receive digest — abort before news API.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_AbortSilently_WhenUserHasNoDeliverableEmail()
    {
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        store.Setup(s => s.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "   ", Name = "X" });

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(store.Object, articles.Object);

        await sut.SendAsync(1, 2, DateTime.UtcNow);

        articles.Verify(
            a => a.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Missing news profile for the user/news pair aborts before fetching articles.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_AbortSilently_WhenNewsProfileMissing()
    {
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        store.Setup(s => s.GetUserEntityByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "a@b.c", Name = "Anna" });
        store.Setup(s => s.GetNewsByIdAsync(1, 88, It.IsAny<CancellationToken>()))
            .ReturnsAsync((News?)null);

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(store.Object, articles.Object);

        await sut.SendAsync(1, 88, DateTime.UtcNow);

        articles.Verify(
            a => a.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// When filters normalize to nothing useful (e.g. blank keywords, empty lists), do not call the remote news API.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_NotCallNewsApi_WhenFiltersProduceNoQuery()
    {
        // Arrange — news row exists but yields empty effective query after trimming/filtering
        News news = new()
        {
            Id = 3,
            UserId = 1,
            Keywords = ["   "],
            Countries = [],
            Languages = [],
            Category = [],
        };
        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        store.Setup(s => s.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "user@test.example", Name = "U" });
        store.Setup(s => s.GetNewsByIdAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<INewsArticleProvider> articles = new();
        NewsletterDigestService sut = CreateSut(store.Object, articles.Object);

        // Act
        await sut.SendAsync(1, 3, DateTime.UtcNow);

        // Assert
        articles.Verify(
            a => a.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Happy path: fetch articles, send email, write Sent notification log with correct metadata.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_SendMail_WriteSentLog_WhenPipelineSucceeds()
    {
        // Arrange
        NewsArticleQuery? capturedQuery = null;
        NotificationLog? capturedLog = null;

        News news = new()
        {
            Id = 12,
            UserId = 2,
            Keywords = ["Berlin"],
        };

        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        store.Setup(s => s.GetUserEntityByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 2, Email = "digest@test.example", Name = "Dieter" });
        store.Setup(s => s.GetNewsByIdAsync(2, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<INewsArticleProvider> articles = new();
        articles.Setup(a => a.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()))
            .Callback<NewsArticleQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync([]);

        Mock<IEmailSender> email = new();
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        store.Setup(s => s.SetNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationLog, CancellationToken>((log, _) => capturedLog = log)
            .Returns(Task.CompletedTask);

        NewsletterDigestService sut = CreateSut(store.Object, articles.Object, email.Object);

        // Act
        await sut.SendAsync(2, 12, new DateTime(2026, 8, 1, 11, 0, 0, DateTimeKind.Utc));

        // Assert — query carries API key and keyword; log marks Sent
        Assert.NotNull(capturedQuery);
        Assert.Equal("integration-test-api-key", capturedQuery!.ApiKey);
        Assert.Equal("Berlin", capturedQuery.KeywordsQuery);

        Assert.NotNull(capturedLog);
        Assert.Equal(2, capturedLog!.UserId);
        Assert.Equal(12, capturedLog.NewsId);
        Assert.Equal(NotificationStatus.Sent, capturedLog.Status);
        Assert.Equal(DeliveryChannel.Email, capturedLog.Channel);

        email.Verify(
            e => e.SendAsync(
                It.Is<EmailMessage>(m =>
                    m.To.Address == "digest@test.example"
                    && m.Subject.Contains("#12", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// When SMTP fails, persist Failed status with error message and rethrow so callers (e.g. Hangfire) can retry or alert.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_WriteFailedLog_AndPropagate_WhenSmtpFails()
    {
        News news = new() { Id = 1, UserId = 1, Keywords = ["test"] };

        Mock<IHermesDataStore> store = new();
        store.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        store.Setup(s => s.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Email = "fail@test.example", Name = "F" });
        store.Setup(s => s.GetNewsByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(news);

        Mock<INewsArticleProvider> articles = new();
        articles.Setup(a => a.GetLatestAsync(It.IsAny<NewsArticleQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Mock<IEmailSender> email = new();
        email.Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        NotificationLog? capturedFailed = null;
        store.Setup(s => s.SetNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationLog, CancellationToken>((log, _) => capturedFailed = log)
            .Returns(Task.CompletedTask);

        NewsletterDigestService sut = CreateSut(store.Object, articles.Object, email.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SendAsync(1, 1, DateTime.UtcNow));

        Assert.NotNull(capturedFailed);
        Assert.Equal(NotificationStatus.Failed, capturedFailed!.Status);
        Assert.Equal("SMTP unavailable", capturedFailed.ErrorMessage);
    }
}
