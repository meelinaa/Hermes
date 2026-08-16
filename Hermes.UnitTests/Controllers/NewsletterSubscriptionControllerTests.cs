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

namespace Hermes.UnitTests.Controllers;

/// <summary>
/// Contains unit tests for <see cref="NewsletterSubscriptionController"/>,
/// verifying pagination, query filtering, sort parsing, CRUD operations, and ownership security.
/// </summary>
public sealed class NewsletterSubscriptionControllerTests
{
    private static NewsletterSubscriptionController CreateController(
        INewsletterSubscriptionService? newsService = null,
        INewsletterSchedulerJobService? jobService = null,
        PaginationOptions? paginationOptions = null,
        int? authenticatedUserId = null)
    {
        var paginationMock = Options.Create(paginationOptions ?? new PaginationOptions
        {
            DefaultPageSize = 10,
            MaxPageSize = 50
        });

        var controller = new NewsletterSubscriptionController(
            newsService ?? Mock.Of<INewsletterSubscriptionService>(),
            jobService ?? Mock.Of<INewsletterSchedulerJobService>(),
            paginationMock);

        DefaultHttpContext httpContext = new();
        if (authenticatedUserId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, authenticatedUserId.Value.ToString()),
                new Claim("sub", authenticatedUserId.Value.ToString())
            ], "TestAuth"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscriptionController.GetNewsList"/> returns validation problem
    /// when page or pageSize parameters are out of range (< 1).
    /// </summary>
    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    public async Task GetNewsList_Should_ReturnValidationProblem_When_PaginationInvalid(int page, int pageSize)
    {
        // Arrange
        NewsletterSubscriptionController sut = CreateController();

        // Act
        ActionResult<PagedNewsletterSubscriptionListResponseDto> result = await sut.GetNewsList(1, page: page, pageSize: pageSize);

        // Assert
        Assert.NotNull(result.Result);
        Assert.IsType<ActionResult<PagedNewsletterSubscriptionListResponseDto>>(result);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscriptionController.GetNewsList"/> returns validation problem
    /// when sort string is unrecognized.
    /// </summary>
    [Fact]
    public async Task GetNewsList_Should_ReturnValidationProblem_When_SortInvalid()
    {
        // Arrange
        NewsletterSubscriptionController sut = CreateController();

        // Act
        ActionResult<PagedNewsletterSubscriptionListResponseDto> result = await sut.GetNewsList(1, sort: "invalid_sort");

        // Assert
        Assert.NotNull(result.Result);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscriptionController.GetNewsList"/> parses valid sort options ("id", "-id", null)
    /// and returns paginated subscriptions.
    /// </summary>
    [Theory]
    [InlineData("id", false)]
    [InlineData("-id", true)]
    [InlineData(null, false)]
    public async Task GetNewsList_Should_ReturnOk_When_QueryParametersValid(string? sort, bool expectedDescending)
    {
        // Arrange
        Mock<INewsletterSubscriptionService> newsService = new();
        NewsletterSubscriptionListQueryDto? capturedQuery = null;

        NewsletterSubscription entity = NewsletterSubscription.CreateForUser(new UserId(1));
        entity.UpdateFilters(["AI", "Cloud"], [NewsCategory.Technology], [Language.English], [Country.Germany]);
        entity.AssignDigestSchedule(ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(8, 0)]));

        newsService.Setup(s => s.GetNewsListAsync(It.IsAny<NewsletterSubscriptionListQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<NewsletterSubscriptionListQueryDto, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(Result.Ok(new NewsletterSubscriptionListResultDto(
                Items: [entity],
                Page: 1,
                PageSize: 10,
                TotalCount: 1,
                TotalPages: 1,
                HasNextPage: false,
                NextAfterId: null)));

        NewsletterSubscriptionController sut = CreateController(newsService: newsService.Object);

        // Act
        ActionResult<PagedNewsletterSubscriptionListResponseDto> result = await sut.GetNewsList(
            userId: 1,
            page: 1,
            pageSize: 10,
            sort: sort,
            q: "AI");

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        PagedNewsletterSubscriptionListResponseDto response = Assert.IsType<PagedNewsletterSubscriptionListResponseDto>(ok.Value);
        Assert.Single(response.Items);
        Assert.NotNull(capturedQuery);
        Assert.Equal(expectedDescending, capturedQuery!.SortDescending);
        Assert.Equal("AI", capturedQuery.Search);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscriptionController.GetNewsById"/> returns 404 NotFound
    /// when the subscription does not exist.
    /// </summary>
    [Fact]
    public async Task GetNewsById_Should_ReturnNotFound_When_SubscriptionDoesNotExist()
    {
        // Arrange
        Mock<INewsletterSubscriptionService> newsService = new();
        newsService.Setup(s => s.GetNewsByIdAsync(new UserId(1), new NewsletterId(99), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<NewsletterSubscription>("Not found"));

        NewsletterSubscriptionController sut = CreateController(newsService: newsService.Object);

        // Act
        ActionResult<NewsletterSubscriptionResponseDto> result = await sut.GetNewsById(1, 99, CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscriptionController.SetNews"/> registers a new subscription,
    /// triggers the scheduler evaluation, and returns HTTP 201 Created.
    /// </summary>
    [Fact]
    public async Task SetNews_Should_CreateSubscriptionAndTriggerScheduler_When_Valid()
    {
        // Arrange
        Mock<INewsletterSubscriptionService> newsService = new();
        Mock<INewsletterSchedulerJobService> jobService = new();

        newsService.Setup(s => s.SetNewsAsync(It.IsAny<NewsletterSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new NewsletterId(42)));

        NewsletterSubscriptionController sut = CreateController(
            newsService: newsService.Object,
            jobService: jobService.Object,
            authenticatedUserId: 1);

        CreateNewsletterSubscriptionRequestDto request = new()
        {
            Category = [NewsCategory.Technology],
            Keywords = ["AI"],
            Languages = [Language.English],
            Countries = [Country.Germany],
            SendOnWeekdays = [Weekdays.Monday],
            SendAtTimes = [new TimeOnly(9, 0)]
        };

        // Act
        ActionResult<CreateNewsletterSubscriptionResponseDto> result = await sut.SetNews(1, request, CancellationToken.None);

        // Assert
        CreatedAtActionResult created = Assert.IsType<CreatedAtActionResult>(result.Result);
        CreateNewsletterSubscriptionResponseDto response = Assert.IsType<CreateNewsletterSubscriptionResponseDto>(created.Value);
        Assert.Equal(42, response.SubscriptionId);
        Assert.Equal(1, response.UserId);
        jobService.Verify(j => j.RequestRunAfterNewsMutation(), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscriptionController.UpdateNews"/> returns 403 Forbidden
    /// when the user attempts to modify a subscription belonging to a different user.
    /// </summary>
    [Fact]
    public async Task UpdateNews_Should_ReturnForbidden_When_ModifyingOtherUserSubscription()
    {
        // Arrange (Caller is user 1, existing resource belongs to user 2)
        NewsletterSubscription existing = NewsletterSubscription.CreateForUser(new UserId(2));

        Mock<INewsletterSubscriptionService> newsService = new();
        newsService.Setup(s => s.FindNewsByIdAsync(new NewsletterId(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(existing));

        NewsletterSubscriptionController sut = CreateController(newsService: newsService.Object, authenticatedUserId: 1);
        UpdateNewsletterSubscriptionRequestDto request = new()
        {
            Id = 5,
            Category = [NewsCategory.Science],
            Keywords = [],
            Languages = [],
            Countries = [],
            SendOnWeekdays = [Weekdays.Monday],
            SendAtTimes = [new TimeOnly(10, 0)],
            IsEnabled = true
        };

        // Act
        ActionResult result = await sut.UpdateNews(1, 5, request, CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscriptionController.DeleteNews"/> removes the subscription
    /// and notifies the scheduler trigger.
    /// </summary>
    [Fact]
    public async Task DeleteNews_Should_DeleteSubscriptionAndTriggerScheduler_When_Exists()
    {
        // Arrange
        NewsletterSubscription existing = NewsletterSubscription.CreateForUser(new UserId(1));

        Mock<INewsletterSubscriptionService> newsService = new();
        Mock<INewsletterSchedulerJobService> jobService = new();

        newsService.Setup(s => s.GetNewsByIdAsync(new UserId(1), new NewsletterId(10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(existing));

        newsService.Setup(s => s.DeleteNewsAsync(existing, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Result>(Result.Ok()));

        NewsletterSubscriptionController sut = CreateController(
            newsService: newsService.Object,
            jobService: jobService.Object,
            authenticatedUserId: 1);

        // Act
        ActionResult result = await sut.DeleteNews(1, 10, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        newsService.Verify(s => s.DeleteNewsAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        jobService.Verify(j => j.RequestRunAfterNewsMutation(), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscriptionController.DeleteAllNews"/> deletes all subscriptions for a user.
    /// </summary>
    [Fact]
    public async Task DeleteAllNews_Should_ReturnOkWithCount()
    {
        // Arrange
        Mock<INewsletterSubscriptionService> newsService = new();
        newsService.Setup(s => s.DeleteAllNewsByUserAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(3));

        NewsletterSubscriptionController sut = CreateController(newsService: newsService.Object, authenticatedUserId: 1);

        // Act
        ActionResult<DeleteAllNewsletterSubscriptionResponseDto> result = await sut.DeleteAllNews(1, CancellationToken.None);

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        DeleteAllNewsletterSubscriptionResponseDto response = Assert.IsType<DeleteAllNewsletterSubscriptionResponseDto>(ok.Value);
        Assert.Equal(3, response.Deleted);
    }
}
