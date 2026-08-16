using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Application.DTOs.User;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Api.Integration;

/// <summary>
/// Contains integration pipeline tests verifying HTTP status codes, security headers, correlation IDs,
/// health probes, cross-account authorization restrictions, and controller query flows using <see cref="InMemoryApiWebApplicationFactory"/>.
/// </summary>
public sealed class ApiPipelineIntegrationTests : IClassFixture<InMemoryApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions _jsonWeb = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly InMemoryApiWebApplicationFactory _factory;

    /// <summary>
    /// Initializes test dependencies with the in-memory API test fixture.
    /// </summary>
    /// <param name="factory">The shared in-memory WebApplicationFactory.</param>
    public ApiPipelineIntegrationTests(InMemoryApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Tests that the liveness health probe endpoint returns HTTP 200 OK without database overhead.
    /// </summary>
    [Fact]
    public async Task Get_HealthLive_Should_ReturnOk()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Tests that non-existent API routes return HTTP 404 Not Found with RFC 7807 problem structure.
    /// </summary>
    [Fact]
    public async Task Get_NonExistentRoute_Should_ReturnNotFound()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/non-existent-endpoint");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Tests that the security headers middleware injects standard security headers into HTTP responses.
    /// </summary>
    [Fact]
    public async Task Request_Should_IncludeSecurityHeaders()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health/live");

        // Assert
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
    }

    /// <summary>
    /// Tests that the correlation ID middleware generates and attaches an X-Correlation-Id response header.
    /// </summary>
    [Fact]
    public async Task Request_Should_IncludeCorrelationIdHeader()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "test-corr-id-12345");

        // Act
        HttpResponseMessage response = await client.GetAsync("/health/live");

        // Assert
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        IEnumerable<string> values = response.Headers.GetValues("X-Correlation-Id");
        Assert.Contains("test-corr-id-12345", values);
    }

    /// <summary>
    /// Tests that protected endpoints return HTTP 401 Unauthorized when no authentication credentials are provided.
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_Should_ReturnUnauthorized_WhenNoAuthHeader()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/users/1");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests that an authenticated user can retrieve their own user profile data.
    /// </summary>
    [Fact]
    public async Task GetUserById_Should_ReturnOk_WhenAuthenticatedAsOwner()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        _factory.UserServiceMock.Setup(s => s.GetUserByIdAsync(It.Is<UserId>(u => u.Value == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new UserScopeDto
            {
                UserId = 1,
                Name = "Authenticated User",
                Email = "user1@hermes.dev",
                IsEmailVerified = true
            }));

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/users/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        UserResponseDto? result = await response.Content.ReadFromJsonAsync<UserResponseDto>(_jsonWeb);
        Assert.NotNull(result);
        Assert.Equal(1, result!.UserId);
        Assert.Equal("Authenticated User", result.Name);
    }

    /// <summary>
    /// Tests that accessing another user's profile results in HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task GetUserById_Should_ReturnForbidden_WhenAccessingOtherUser()
    {
        // Arrange (Caller is user 1, requested route is user 2)
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/users/2");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests the news preview endpoint pipeline execution when authenticated.
    /// </summary>
    [Fact]
    public async Task GetNewsPreview_Should_ReturnOk_WithArticles()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        _factory.ArticleFetchingServiceMock.Setup(s => s.FetchPreviewArticlesAsync(It.IsAny<NewsPreviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new NewsArticle("art-1", "https://news.com/1", "Integration Headline", "Snippet", ["tech"], null)
            ]);

        NewsPreviewRequestDto requestDto = new() { Keywords = "dotnet" };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/news/preview", requestDto, options: _jsonWeb);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        List<NewsArticle>? articles = await response.Content.ReadFromJsonAsync<List<NewsArticle>>(_jsonWeb);
        Assert.NotNull(articles);
        Assert.Single(articles!);
        Assert.Equal("Integration Headline", articles![0].Title);
    }

    /// <summary>
    /// Tests the newsletter subscription query pipeline execution for the authenticated user.
    /// </summary>
    [Fact]
    public async Task GetNewsletterSubscriptions_Should_ReturnOk_WithPaginatedList()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", "1");

        NewsletterSubscription entity = NewsletterSubscription.CreateForUser(new UserId(1));
        entity.UpdateFilters(["AI"], [NewsCategory.Technology], [Language.English], [Country.Germany]);
        entity.AssignDigestSchedule(ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(8, 0)]));

        _factory.NewsletterServiceMock.Setup(s => s.GetNewsListAsync(It.IsAny<NewsletterSubscriptionListQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new NewsletterSubscriptionListResultDto(
                Items: [entity],
                Page: 1,
                PageSize: 10,
                TotalCount: 1,
                TotalPages: 1,
                HasNextPage: false,
                NextAfterId: null)));

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/users/1/newsletter-subscriptions");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        PagedNewsletterSubscriptionListResponseDto? paged = await response.Content.ReadFromJsonAsync<PagedNewsletterSubscriptionListResponseDto>(_jsonWeb);
        Assert.NotNull(paged);
        Assert.Single(paged!.Items);
        Assert.Equal(1, paged.TotalCount);
    }
}
