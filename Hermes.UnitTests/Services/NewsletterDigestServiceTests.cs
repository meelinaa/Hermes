using FluentResults;
using Hermes.Application.DTOs.Email;
using Hermes.Application.DTOs.NewsArticle;
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

    [Fact]
    public async Task SendAsync_Should_Fail_WhenUserOrNewsIdNotPositive()
    {
        var sut = new NewsletterDigestService(Mock.Of<IUserRepository>(), Mock.Of<INewsletterSubscriptionRepository>(), Mock.Of<IArticleFetchingService>(), Mock.Of<IEmailProvider>(), Mock.Of<INewsletterHtmlService>(), TimeProvider.System);

        var result1 = await sut.SendAsync(new UserId(0), new NewsletterId(1), DateTime.UtcNow);
        var result2 = await sut.SendAsync(new UserId(1), new NewsletterId(-1), DateTime.UtcNow);

        Assert.True(result1.IsFailed);
        Assert.True(result2.IsFailed);
    }

    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenUserOrEmailMissing()
    {
        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(new UserId(7), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var sut = new NewsletterDigestService(users.Object, Mock.Of<INewsletterSubscriptionRepository>(), Mock.Of<IArticleFetchingService>(), Mock.Of<IEmailProvider>(), Mock.Of<INewsletterHtmlService>(), TimeProvider.System);

        var result = await sut.SendAsync(new UserId(7), new NewsletterId(99), DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenNewsDisabled()
    {
        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(1), Email = Email.Parse("off@b.c"), Name = "Off" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        newsPort.Setup(store => store.GetNewsByIdAsync(new UserId(1), new NewsletterId(42), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNews(new NewsletterId(42), new UserId(1), ["x"], false));
        var sut = new NewsletterDigestService(users.Object, newsPort.Object, Mock.Of<IArticleFetchingService>(), Mock.Of<IEmailProvider>(), Mock.Of<INewsletterHtmlService>(), TimeProvider.System);

        var result = await sut.SendAsync(new UserId(1), new NewsletterId(42), DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenNoArticlesFound()
    {
        Mock<IUserRepository> users = new();
        users.Setup(u => u.GetUserEntityByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(5), Name = "Alice", Email = Email.Parse("alice@test.com") });
        Mock<INewsletterSubscriptionRepository> newsStore = new();
        newsStore.Setup(s => s.GetNewsByIdAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNews(new NewsletterId(10), new UserId(5), ["test"], true));
        Mock<IArticleFetchingService> articleFetchingService = new();
        articleFetchingService.Setup(p => p.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NewsArticle>());

        var sut = new NewsletterDigestService(users.Object, newsStore.Object, articleFetchingService.Object, Mock.Of<IEmailProvider>(), Mock.Of<INewsletterHtmlService>(), TimeProvider.System);

        var result = await sut.SendAsync(new UserId(5), new NewsletterId(10), DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
    }

    [Fact]
    public async Task SendAsync_Should_ReturnTrue_WhenPipelineSucceeds()
    {
        Mock<IUserRepository> users = new();
        users.Setup(store => store.GetUserEntityByIdAsync(new UserId(2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(2), Email = Email.Parse("digest@test.example"), Name = "Dieter" });
        Mock<INewsletterSubscriptionRepository> newsPort = new();
        newsPort.Setup(store => store.GetNewsByIdAsync(new UserId(2), new NewsletterId(12), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateNews(new NewsletterId(12), new UserId(2), ["Berlin"]));
        Mock<IArticleFetchingService> articles = new();
        articles.Setup(articleProvider => articleProvider.FetchArticlesForSubscriptionAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new NewsArticle("1", "A", "desc", "http://a", null, null)]);
        Mock<IEmailProvider> email = new();
        email.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<INewsletterHtmlService> renderer = new();
        renderer.Setup(r => r.RenderNewsletterAsync(It.IsAny<NewsletterRenderRequestDto>(), It.IsAny<CancellationToken>())).ReturnsAsync("html");

        var sut = new NewsletterDigestService(users.Object, newsPort.Object, articles.Object, email.Object, renderer.Object, TimeProvider.System);

        var result = await sut.SendAsync(new UserId(2), new NewsletterId(12), DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }
}

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

    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenDuplicateAlreadySentInWindow()
    {
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.ExistsSentNotificationInWindowAsync(
                It.IsAny<UserId>(),
                It.IsAny<NewsletterId>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Mock<INewsletterDigestService> inner = new();

        var sut = new NewsletterDigestLoggingDecorator(inner.Object, logs.Object, CreateDefaultNewsStore(), Options.Create(new NewsletterOptions()), TimeProvider.System, Mock.Of<ILogger<NewsletterDigestLoggingDecorator>>());

        var result = await sut.SendAsync(new UserId(5), new NewsletterId(10), DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
        inner.Verify(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_Should_LogSuccess_WhenInnerSucceeds()
    {
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.SetNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        Mock<INewsletterDigestService> inner = new();
        inner.Setup(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok(true));

        var sut = new NewsletterDigestLoggingDecorator(inner.Object, logs.Object, CreateDefaultNewsStore(), Options.Create(new NewsletterOptions()), TimeProvider.System, Mock.Of<ILogger<NewsletterDigestLoggingDecorator>>());

        var result = await sut.SendAsync(new UserId(2), new NewsletterId(12), DateTime.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        logs.Verify(l => l.SetNotificationLogAsync(It.Is<NotificationLog>(n => n.Status == NotificationStatus.Sent), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_Should_LogErrorAndThrow_WhenInnerThrows()
    {
        Mock<INotificationLogRepository> logs = new();
        logs.Setup(s => s.SetNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>())).Returns(ValueTask.CompletedTask);
        Mock<INewsletterDigestService> inner = new();
        inner.Setup(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("SMTP unavailable"));

        var sut = new NewsletterDigestLoggingDecorator(inner.Object, logs.Object, CreateDefaultNewsStore(), Options.Create(new NewsletterOptions()), TimeProvider.System, Mock.Of<ILogger<NewsletterDigestLoggingDecorator>>());

        var exception = await Assert.ThrowsAsync<Exception>(() => sut.SendAsync(new UserId(1), new NewsletterId(1), DateTime.UtcNow));

        Assert.Equal("SMTP unavailable", exception.Message);
        logs.Verify(l => l.SetNotificationLogAsync(It.Is<NotificationLog>(n => n.Status == NotificationStatus.Failed && n.ErrorMessage == "SMTP unavailable"), It.IsAny<CancellationToken>()), Times.Once);
    }
}




