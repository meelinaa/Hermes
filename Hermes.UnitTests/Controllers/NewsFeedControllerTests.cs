using Hermes.Api.Controllers.Newsletter;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Services.Newsletter;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Controllers;

/// <summary>
/// Unit tests for <see cref="NewsFeedController"/> verifying live article preview endpoints.
/// </summary>
public sealed class NewsFeedControllerTests
{
    private readonly Mock<IArticleFetchingService> _articleFetchingServiceMock = new();
    private readonly NewsFeedController _sut;

    public NewsFeedControllerTests()
    {
        _sut = new NewsFeedController(_articleFetchingServiceMock.Object);
    }

    [Fact]
    public async Task GetNewsPreview_Should_Return_Ok_With_Articles()
    {
        // Arrange
        NewsPreviewRequestDto request = new()
        {
            Keywords = "AI, Cloud"
        };

        List<NewsArticle> articles =
        [
            new("art-1", "https://example.com/1", "AI Breakthrough", "Summary 1", ["technology"], "https://img.com/1.jpg"),
            new("art-2", "https://example.com/2", "Cloud Innovations", "Summary 2", ["business"], null)
        ];

        _articleFetchingServiceMock
            .Setup(s => s.FetchPreviewArticlesAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(articles);

        // Act
        ActionResult<IReadOnlyList<NewsArticle>> result = await _sut.GetNewsPreview(request, CancellationToken.None);

        // Assert
        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result.Result);
        IReadOnlyList<NewsArticle> returnedArticles = Assert.IsAssignableFrom<IReadOnlyList<NewsArticle>>(okResult.Value);
        Assert.Equal(2, returnedArticles.Count);
        Assert.Equal("AI Breakthrough", returnedArticles[0].Title);
    }
}
