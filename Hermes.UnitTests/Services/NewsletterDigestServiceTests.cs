using FluentResults;
using Hermes.Application.DTOs.Email;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Newsletter;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Contains unit tests for <see cref="NewsletterDigestService"/> and <see cref="NewsletterDigestLoggingDecorator"/>,
/// verifying content truncation, HTML rendering requests, recipient resolution, and duplicate dispatch prevention.
/// </summary>
public sealed class NewsletterDigestServiceTests
{
    private static NewsletterSubscription CreateNews(NewsletterId id, UserId userId, string[]? keywords = null, bool isEnabled = true)
    {
        var news = NewsletterSubscription.CreateForUser(userId);
        news.UpdateFilters(keywords, null, null, null);
        if (!isEnabled) news.Disable();
        news.SetId(id);
        return news;
    }

    /// <summary>
    /// Tests that <see cref="NewsletterDigestService.SendAsync"/> fails when either the user ID or newsletter ID is non-positive.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task SendAsync_Should_Fail_WhenUserOrNewsIdNotPositive(int userId, int newsId)
    {
        // Arrange
        var sut = new NewsletterDigestService(
            Mock.Of<IUserStore>(),
            Mock.Of<INewsletterSubscriptionStore>(),
            Mock.Of<IArticleFetchingService>(),
            Mock.Of<IEmailProvider>(),
            Mock.Of<INewsletterHtmlService>(),
            TimeProvider.System);

        // Act
        var result = await sut.SendAsync(new UserId(userId), new NewsletterId(newsId), DateTime.UtcNow);

        // Assert
        Assert.True(result.IsFailed);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterDigestService.SendAsync"/> returns Ok(false) when the user does not exist or has an empty email.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenUserOrEmailMissing()
    {
        // Arrange
        Mock<IUserStore> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(new UserId(7), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var sut = new NewsletterDigestService(
            users.Object,
            Mock.Of<INewsletterSubscriptionStore>(),
            Mock.Of<IArticleFetchingService>(),
            Mock.Of<IEmailProvider>(),
            Mock.Of<INewsletterHtmlService>(),
            TimeProvider.System);

        // Act
        var result = await sut.SendAsync(new UserId(7), new NewsletterId(99), DateTime.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterDigestService.SendAsync"/> returns Ok(false) when the newsletter subscription is disabled.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenNewsDisabled()
    {
        // Arrange
        Mock<IUserStore> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = Email.Parse("off@b.c"), Name = "Off" });
        Mock<INewsletterSubscriptionStore> newsPort = new();
        newsPort.Setup(store => store.GetNewsByIdAsync(new UserId(1), new NewsletterId(42), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNews(new NewsletterId(42), new UserId(1), ["x"], isEnabled: false));

        var sut = new NewsletterDigestService(
            users.Object,
            newsPort.Object,
            Mock.Of<IArticleFetchingService>(),
            Mock.Of<IEmailProvider>(),
            Mock.Of<INewsletterHtmlService>(),
            TimeProvider.System);

        // Act
        var result = await sut.SendAsync(new UserId(1), new NewsletterId(42), DateTime.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterDigestService.SendAsync"/> returns Ok(false) when no articles are available.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenNoArticlesFound()
    {
        // Arrange
        Mock<IUserStore> users = new();
        users.Setup(u => u.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(5), Name = "Alice", Email = Email.Parse("alice@test.com") });
        Mock<INewsletterSubscriptionStore> newsStore = new();
        newsStore.Setup(s => s.GetNewsByIdAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNews(new NewsletterId(10), new UserId(5), ["test"], isEnabled: true));
        Mock<IArticleFetchingService> articleFetchingService = new();
        articleFetchingService.Setup(p => p.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NewsArticle>());

        var sut = new NewsletterDigestService(
            users.Object,
            newsStore.Object,
            articleFetchingService.Object,
            Mock.Of<IEmailProvider>(),
            Mock.Of<INewsletterHtmlService>(),
            TimeProvider.System);

        // Act
        var result = await sut.SendAsync(new UserId(5), new NewsletterId(10), DateTime.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterDigestService.DeduplicateArticles"/> eliminates tracking parameters (utm_source, ref) and removes duplicate URLs.
    /// </summary>
    [Fact]
    public void DeduplicateArticles_Should_StripTrackingParamsAndRemoveDuplicateUrls()
    {
        // Arrange
        List<NewsArticle> articles =
        [
            new("1", "https://example.com/article1?utm_source=twitter&utm_medium=social", "Title One Long Headline Here", "Desc", ["tech"], null),
            new("2", "https://example.com/article1?ref=homepage&fbclid=12345", "Title One Long Headline Here", "Desc", ["tech"], null),
            new("3", "https://example.com/article2", "Title Two Long Headline Here", "Desc", ["tech"], null)
        ];

        // Act
        var deduplicated = NewsletterDigestService.DeduplicateArticles(articles);

        // Assert
        Assert.Equal(2, deduplicated.Count);
        Assert.Equal("1", deduplicated[0].ArticleId);
        Assert.Equal("3", deduplicated[1].ArticleId);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterDigestService.DeduplicateArticles"/> avoids false positive collisions for distinct articles with short generic titles.
    /// </summary>
    [Fact]
    public void DeduplicateArticles_Should_NotDeduplicateShortGenericTitlesFromDifferentUrls()
    {
        // Arrange
        List<NewsArticle> articles =
        [
            new("1", "https://spiegel.de/live-ticker-1", "Live-Ticker", "Desc 1", ["news"], null),
            new("2", "https://zeit.de/live-ticker-2", "Live-Ticker", "Desc 2", ["news"], null),
            new("3", "https://focus.de/eilmeldung", "Eilmeldung", "Desc 3", ["news"], null)
        ];

        // Act
        var deduplicated = NewsletterDigestService.DeduplicateArticles(articles);

        // Assert: Short generic titles must NOT be eliminated across distinct URLs
        Assert.Equal(3, deduplicated.Count);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterDigestService.SendAsync"/> limits articles to 5 items,
    /// truncates descriptions longer than 150 characters with an ellipsis, falls back to default values for null metadata,
    /// renders HTML, and sends the email message.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ProcessAndSendEmail_WhenArticlesAvailable()
    {
        // Arrange
        Mock<IUserStore> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(new UserId(2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(2), Email = Email.Parse("digest@test.example"), Name = "Dieter" });

        Mock<INewsletterSubscriptionStore> newsPort = new();
        newsPort.Setup(store => store.GetNewsByIdAsync(new UserId(2), new NewsletterId(12), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNews(new NewsletterId(12), new UserId(2), ["Berlin"]));

        string longDescription = new('A', 200);
        List<NewsArticle> fetchedArticles = [
            new("1", "http://link1", "Title 1 Long Unique Headline Text", longDescription, ["technology"], "http://img1"),
            new("2", null, null, null, null, null), // Exercise all null fallbacks
            new("3", "http://link3", "Title 3 Long Unique Headline Text", "Short description", ["science"], null),
            new("4", "http://link4", "Title 4 Long Unique Headline Text", "Desc 4", null, null),
            new("5", "http://link5", "Title 5 Long Unique Headline Text", "Desc 5", null, null),
            new("6", "http://link6", "Title 6 Long Unique Headline Text", "Desc 6", null, null) // 6th article must be excluded (max 5)
        ];

        Mock<IArticleFetchingService> articles = new();
        articles.Setup(articleProvider => articleProvider.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fetchedArticles);

        NewsletterRenderRequestDto? capturedRenderRequest = null;
        Mock<INewsletterHtmlService> renderer = new();
        renderer.Setup(r => r.RenderNewsletterAsync(It.IsAny<NewsletterRenderRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<NewsletterRenderRequestDto, CancellationToken>((req, _) => capturedRenderRequest = req)
            .ReturnsAsync("<html>newsletter</html>");

        EmailMessageDto? capturedEmail = null;
        Mock<IEmailProvider> email = new();
        email.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessageDto, CancellationToken>((msg, _) => capturedEmail = msg)
            .Returns(Task.CompletedTask);

        var sut = new NewsletterDigestService(users.Object, newsPort.Object, articles.Object, email.Object, renderer.Object, TimeProvider.System);

        // Act
        var result = await sut.SendAsync(new UserId(2), new NewsletterId(12), DateTime.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.NotNull(capturedRenderRequest);
        Assert.Equal(5, capturedRenderRequest!.Articles.Count);

        // Verify truncation of 1st article
        Assert.Equal(150, capturedRenderRequest.Articles[0].Content.Length);
        Assert.EndsWith("...", capturedRenderRequest.Articles[0].Content);

        // Verify null fallbacks of 2nd article
        Assert.Equal("News", capturedRenderRequest.Articles[1].Category);
        Assert.Equal(string.Empty, capturedRenderRequest.Articles[1].Title);
        Assert.Equal(string.Empty, capturedRenderRequest.Articles[1].Content);
        Assert.Equal("#", capturedRenderRequest.Articles[1].Url);
        Assert.Equal(string.Empty, capturedRenderRequest.Articles[1].ImageUrl);

        Assert.NotNull(capturedEmail);
        Assert.Equal("Dieter", capturedEmail!.To.DisplayName);
        Assert.Equal("digest@test.example", capturedEmail.To.Address);
    }
}

/// <summary>
/// Contains unit tests for <see cref="NewsletterDigestLoggingDecorator"/>,
/// verifying atomic slot reservations, duplicate skip, success audits, crash recovery, and quota handling.
/// </summary>
public sealed class NewsletterDigestLoggingDecoratorTests
{
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
    /// Tests that the decorator skips invocation and returns Ok(false) when a duplicate notification was already sent.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenDuplicateAlreadySentInWindow()
    {
        // Arrange
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.TryReserveSlotAsync(
                It.IsAny<NotificationLog>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SlotReservationResult.AlreadySent());

        Mock<INewsletterDigestService> inner = new();

        var sut = new NewsletterDigestLoggingDecorator(
            inner.Object,
            logs.Object,
            CreateDefaultNewsStore(),
            Options.Create(new NewsletterOptions()),
            TimeProvider.System,
            Mock.Of<ILogger<NewsletterDigestLoggingDecorator>>());

        // Act
        var result = await sut.SendAsync(new UserId(5), new NewsletterId(10), DateTime.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
        inner.Verify(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that the decorator reserves a pending slot, executes inner send, and updates status to Sent.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_LogSuccess_WhenInnerSucceeds()
    {
        // Arrange
        var createdLog = NotificationLog.Create(new UserId(2), DeliveryChannel.Email, DateTime.UtcNow, new NewsletterId(12));
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.TryReserveSlotAsync(
                It.IsAny<NotificationLog>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SlotReservationResult.NewReservation(createdLog));

        logs.Setup(s => s.UpdateNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        Mock<INewsletterDigestService> inner = new();
        inner.Setup(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(true));

        var sut = new NewsletterDigestLoggingDecorator(
            inner.Object,
            logs.Object,
            CreateDefaultNewsStore(),
            Options.Create(new NewsletterOptions()),
            TimeProvider.System,
            Mock.Of<ILogger<NewsletterDigestLoggingDecorator>>());

        // Act
        var result = await sut.SendAsync(new UserId(2), new NewsletterId(12), DateTime.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        logs.Verify(l => l.UpdateNotificationLogAsync(It.Is<NotificationLog>(n => n.Status == NotificationStatus.Sent), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that the decorator records a failed notification log entry with error details when an unhandled exception is thrown.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_LogErrorAndThrow_WhenInnerThrows()
    {
        // Arrange
        var createdLog = NotificationLog.Create(new UserId(1), DeliveryChannel.Email, DateTime.UtcNow, new NewsletterId(1));
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.TryReserveSlotAsync(
                It.IsAny<NotificationLog>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SlotReservationResult.NewReservation(createdLog));

        logs.Setup(s => s.UpdateNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        Mock<INewsletterDigestService> inner = new();
        inner.Setup(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SMTP unavailable"));

        var sut = new NewsletterDigestLoggingDecorator(
            inner.Object,
            logs.Object,
            CreateDefaultNewsStore(),
            Options.Create(new NewsletterOptions()),
            TimeProvider.System,
            Mock.Of<ILogger<NewsletterDigestLoggingDecorator>>());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => sut.SendAsync(new UserId(1), new NewsletterId(1), DateTime.UtcNow));

        Assert.Equal("SMTP unavailable", exception.Message);
        logs.Verify(l => l.UpdateNotificationLogAsync(It.Is<NotificationLog>(n => n.Status == NotificationStatus.Failed && n.ErrorMessage == "SMTP unavailable"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that when DailyQuotaExceededException occurs, the decorator catches it terminally, marks log Failed, advances slot, and returns Result.Fail without rethrowing.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_HandleDailyQuotaExceededTerminally_WithoutRethrowing()
    {
        // Arrange
        var createdLog = NotificationLog.Create(new UserId(1), DeliveryChannel.Email, DateTime.UtcNow, new NewsletterId(1));
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.TryReserveSlotAsync(
                It.IsAny<NotificationLog>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SlotReservationResult.NewReservation(createdLog));

        logs.Setup(s => s.UpdateNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        Mock<INewsletterSubscriptionRepository> newsStore = new();
        newsStore.Setup(s => s.AdvanceNextDigestSlotAsync(
                It.IsAny<NewsletterId>(),
                It.IsAny<UserId>(),
                It.IsAny<TimeZoneInfo>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        Mock<INewsletterDigestService> inner = new();
        inner.Setup(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DailyQuotaExceededException("Daily quota exceeded"));

        var sut = new NewsletterDigestLoggingDecorator(
            inner.Object,
            logs.Object,
            newsStore.Object,
            Options.Create(new NewsletterOptions()),
            TimeProvider.System,
            Mock.Of<ILogger<NewsletterDigestLoggingDecorator>>());

        // Act
        var result = await sut.SendAsync(new UserId(1), new NewsletterId(1), DateTime.UtcNow);

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("Daily quota exceeded", result.Errors[0].Message);
        newsStore.Verify(s => s.AdvanceNextDigestSlotAsync(new NewsletterId(1), new UserId(1), It.IsAny<TimeZoneInfo>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        logs.Verify(l => l.UpdateNotificationLogAsync(It.Is<NotificationLog>(n => n.Status == NotificationStatus.Failed), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that the decorator reclaims a stale pending lease from a crashed attempt and delivers the email.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ReclaimPendingAndSend_WhenPreviousAttemptCrashed()
    {
        // Arrange
        var stalePendingLog = NotificationLog.Create(new UserId(3), DeliveryChannel.Email, DateTime.UtcNow.AddMinutes(-2), new NewsletterId(33));
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.TryReserveSlotAsync(
                It.IsAny<NotificationLog>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SlotReservationResult.Reclaimed(stalePendingLog));

        logs.Setup(s => s.UpdateNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        Mock<INewsletterDigestService> inner = new();
        inner.Setup(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(true));

        var sut = new NewsletterDigestLoggingDecorator(
            inner.Object,
            logs.Object,
            CreateDefaultNewsStore(),
            Options.Create(new NewsletterOptions()),
            TimeProvider.System,
            Mock.Of<ILogger<NewsletterDigestLoggingDecorator>>());

        // Act
        var result = await sut.SendAsync(new UserId(3), new NewsletterId(33), DateTime.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        inner.Verify(i => i.SendAsync(new UserId(3), new NewsletterId(33), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        logs.Verify(l => l.UpdateNotificationLogAsync(It.Is<NotificationLog>(n => n.Status == NotificationStatus.Sent), It.IsAny<CancellationToken>()), Times.Once);
    }
}
