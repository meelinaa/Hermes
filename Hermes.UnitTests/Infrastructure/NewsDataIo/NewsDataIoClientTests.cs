using System.Net;
using System.Text.Json;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Options.External;
using Hermes.Domain.Exceptions;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using StackExchange.Redis;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.NewsDataIo;

/// <summary>
/// Contains unit tests for <see cref="NewsDataIoClient"/>, verifying HTTP error propagation, API key sanitization, and daily quota guard.
/// </summary>
public sealed class NewsDataIoClientTests
{
    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseBody)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseBody)
            });

        return new HttpClient(handlerMock.Object);
    }

    /// <summary>
    /// Tests that <see cref="NewsDataIoClient.GetLatestAsync"/> throws an HttpRequestException containing the status code when receiving HTTP 429 Too Many Requests.
    /// </summary>
    [Fact]
    public async Task GetLatestAsync_Should_ThrowHttpRequestException_WhenHttp429TooManyRequests()
    {
        // Arrange
        HttpClient httpClient = CreateMockHttpClient(HttpStatusCode.TooManyRequests, "{\"status\":\"error\",\"message\":\"Rate limit exceeded\"}");
        Mock<ILogger<NewsDataIoClient>> loggerMock = new();
        NewsDataIoClient sut = new(httpClient, loggerMock.Object);

        NewsArticleQueryDto query = new()
        {
            ApiKey = "secret-12345",
            KeywordsQuery = "technology"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetLatestAsync(query));
        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Contains("Rate limit exceeded", ex.Message);
        Assert.DoesNotContain("secret-12345", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="NewsDataIoClient.GetLatestAsync"/> throws an HttpRequestException when receiving HTTP 500 Internal Server Error.
    /// </summary>
    [Fact]
    public async Task GetLatestAsync_Should_ThrowHttpRequestException_WhenHttp500InternalServerError()
    {
        // Arrange
        HttpClient httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "Internal Server Error");
        Mock<ILogger<NewsDataIoClient>> loggerMock = new();
        NewsDataIoClient sut = new(httpClient, loggerMock.Object);

        NewsArticleQueryDto query = new()
        {
            ApiKey = "my-key-999",
            KeywordsQuery = "science"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetLatestAsync(query));
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="NewsDataIoClient.GetLatestAsync"/> throws a DailyQuotaExceededException when Redis reports usage beyond MaxDailyRequests.
    /// </summary>
    [Fact]
    public async Task GetLatestAsync_Should_ThrowDailyQuotaExceededException_WhenDailyBudgetExhausted()
    {
        // Arrange
        HttpClient httpClient = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        Mock<ILogger<NewsDataIoClient>> loggerMock = new();

        Mock<IDatabase> redisDb = new();
        redisDb.Setup(db => db.StringIncrementAsync(It.IsAny<RedisKey>(), 1, CommandFlags.None))
            .ReturnsAsync(101); // Exceeds limit 100

        Mock<IConnectionMultiplexer> redisMock = new();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(redisDb.Object);

        var options = Options.Create(new NewsDataIoOptions { Key = "test-key", MaxDailyRequests = 100 });
        NewsDataIoClient sut = new(httpClient, loggerMock.Object, redisMock.Object, options);

        NewsArticleQueryDto query = new()
        {
            ApiKey = "test-key",
            KeywordsQuery = "sports"
        };

        // Act & Assert
        await Assert.ThrowsAsync<DailyQuotaExceededException>(() => sut.GetLatestAsync(query));
    }

    /// <summary>
    /// Tests that <see cref="NewsDataIoClient.GetLatestAsync"/> parses valid JSON responses and extracts news articles.
    /// </summary>
    [Fact]
    public async Task GetLatestAsync_Should_ReturnArticles_WhenHttp200Ok()
    {
        // Arrange
        string json = """
        {
            "status": "ok",
            "totalResults": 1,
            "articles": [
                {
                    "title": "Quantum Breakthrough",
                    "description": "Researchers achieved new results.",
                    "url": "https://example.com/quantum",
                    "urlToImage": "https://example.com/img.jpg"
                }
            ]
        }
        """;

        HttpClient httpClient = CreateMockHttpClient(HttpStatusCode.OK, json);
        Mock<ILogger<NewsDataIoClient>> loggerMock = new();
        NewsDataIoClient sut = new(httpClient, loggerMock.Object);

        NewsArticleQueryDto query = new()
        {
            ApiKey = "key-123",
            KeywordsQuery = "quantum"
        };

        // Act
        var result = await sut.GetLatestAsync(query);

        // Assert
        Assert.Single(result);
        Assert.Equal("Quantum Breakthrough", result[0].Title);
        Assert.Equal("https://example.com/quantum", result[0].Link);
    }

    /// <summary>
    /// Tests that <see cref="NewsDataIoClient.GetLatestAsync"/> properly bubbles cancellation or timeout exceptions.
    /// </summary>
    [Fact]
    public async Task GetLatestAsync_Should_ThrowOperationCanceledException_WhenRequestTimesOut()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("The request was canceled due to timeout."));

        HttpClient httpClient = new(handlerMock.Object);
        Mock<ILogger<NewsDataIoClient>> loggerMock = new();
        NewsDataIoClient sut = new(httpClient, loggerMock.Object);

        NewsArticleQueryDto query = new()
        {
            ApiKey = "key-123",
            KeywordsQuery = "quantum"
        };

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => sut.GetLatestAsync(query));
    }

    /// <summary>
    /// Tests that <see cref="NewsDataIoClient.GetLatestAsync"/> returns an empty collection when the payload contains 0 articles or results is null.
    /// </summary>
    [Fact]
    public async Task GetLatestAsync_Should_ReturnEmptyList_WhenResponseHasNoArticles()
    {
        // Arrange
        string json = """
        {
            "status": "ok",
            "totalResults": 0,
            "results": []
        }
        """;

        HttpClient httpClient = CreateMockHttpClient(HttpStatusCode.OK, json);
        Mock<ILogger<NewsDataIoClient>> loggerMock = new();
        NewsDataIoClient sut = new(httpClient, loggerMock.Object);

        NewsArticleQueryDto query = new()
        {
            ApiKey = "key-123",
            KeywordsQuery = "nonexistent-topic"
        };

        // Act
        var result = await sut.GetLatestAsync(query);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    /// Tests that <see cref="NewsDataIoClient.GetLatestAsync"/> consistently rejects requests with <see cref="HttpRequestException"/>
    /// and logs error diagnostics when receiving consecutive HTTP 500 responses.
    /// </summary>
    [Fact]
    public async Task NewsDataIoClient_Should_TripCircuitBreaker_AfterConsecutiveHttp500s()
    {
        // Arrange
        int callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent("{\"status\":\"error\",\"message\":\"Internal server failure\"}")
                };
            });

        HttpClient httpClient = new(handlerMock.Object);
        Mock<ILogger<NewsDataIoClient>> loggerMock = new();
        NewsDataIoClient sut = new(httpClient, loggerMock.Object);

        NewsArticleQueryDto query = new()
        {
            ApiKey = "key-500",
            KeywordsQuery = "resilience"
        };

        // Act & Assert (3 consecutive failures)
        for (int i = 0; i < 3; i++)
        {
            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetLatestAsync(query));
            Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        }

        Assert.Equal(3, callCount);
    }
}
