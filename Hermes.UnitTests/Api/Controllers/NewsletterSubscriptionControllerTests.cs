using System.Security.Claims;
using FluentResults;
using Hermes.Api.Controllers.Newsletter;
using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Application.Options.Common;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Api.Controllers;

/// <summary>
/// Unit tests verifying security, IDOR protection, and endpoint behaviors of <see cref="NewsletterSubscriptionController"/>.
/// </summary>
public sealed class NewsletterSubscriptionControllerTests
{
    private static NewsletterSubscriptionController CreateController(
        INewsletterSubscriptionService newsService,
        INewsletterSchedulerJobService? trigger = null,
        ClaimsPrincipal? user = null)
    {
        NewsletterSubscriptionController controller = new(
            newsService,
            trigger ?? new Mock<INewsletterSchedulerJobService>().Object,
            Options.Create(new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 }));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user ?? new ClaimsPrincipal() }
        };

        return controller;
    }

    private static ClaimsPrincipal CreatePrincipalWithId(int userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "TestAuthType"));
    }

    [Fact]
    public async Task UpdateNews_Should_ReturnForbidden_When_SubscriptionBelongsToDifferentUser()
    {
        // Arrange: User 1 tries to update subscription ID 42 belonging to User 2
        int callerUserId = 1;
        int ownerUserId = 2;
        int newsId = 42;

        Mock<INewsletterSubscriptionService> newsServiceMock = new();
        NewsletterSubscription foreignSubscription = NewsletterSubscription.CreateForUser(new UserId(ownerUserId));
        typeof(NewsletterSubscription).GetProperty("Id")!.SetValue(foreignSubscription, new NewsletterId(newsId));

        newsServiceMock.Setup(s => s.FindNewsByIdAsync(new NewsletterId(newsId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(foreignSubscription));

        ClaimsPrincipal caller = CreatePrincipalWithId(callerUserId);
        NewsletterSubscriptionController sut = CreateController(newsServiceMock.Object, user: caller);

        UpdateNewsletterSubscriptionRequestDto request = new()
        {
            Id = newsId,
            Keywords = ["hacked-keyword"],
            Category = [],
            Languages = [],
            Countries = [],
            SendOnWeekdays = [Weekdays.Monday],
            SendAtTimes = [new TimeOnly(8, 0)]
        };

        // Act
        ActionResult result = await sut.UpdateNews(request, CancellationToken.None);

        // Assert: Access is forbidden and update is never called
        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        newsServiceMock.Verify(s => s.UpdateNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateNews_Should_Succeed_When_SubscriptionBelongsToCaller()
    {
        // Arrange: User 1 updates their own subscription ID 10
        int callerUserId = 1;
        int newsId = 10;

        Mock<INewsletterSubscriptionService> newsServiceMock = new();
        NewsletterSubscription ownSubscription = NewsletterSubscription.CreateForUser(new UserId(callerUserId));
        typeof(NewsletterSubscription).GetProperty("Id")!.SetValue(ownSubscription, new NewsletterId(newsId));

        newsServiceMock.Setup(s => s.FindNewsByIdAsync(new NewsletterId(newsId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(ownSubscription));
        newsServiceMock.Setup(s => s.UpdateNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock<INewsletterSchedulerJobService> triggerMock = new();
        ClaimsPrincipal caller = CreatePrincipalWithId(callerUserId);
        NewsletterSubscriptionController sut = CreateController(newsServiceMock.Object, trigger: triggerMock.Object, user: caller);

        UpdateNewsletterSubscriptionRequestDto request = new()
        {
            Id = newsId,
            Keywords = ["valid-keyword"],
            Category = [],
            Languages = [],
            Countries = [],
            SendOnWeekdays = [Weekdays.Monday],
            SendAtTimes = [new TimeOnly(8, 0)]
        };

        // Act
        ActionResult result = await sut.UpdateNews(request, CancellationToken.None);

        // Assert
        Assert.IsType<OkResult>(result);
        newsServiceMock.Verify(s => s.UpdateNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()), Times.Once);
        triggerMock.Verify(t => t.RequestRunAfterNewsMutation(), Times.Once);
    }
}
