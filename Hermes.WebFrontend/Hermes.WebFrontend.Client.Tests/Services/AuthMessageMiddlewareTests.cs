using System.Net;
using Blazored.LocalStorage;
using Hermes.WebFrontend.Client.Services.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Services;

/// <summary>
/// Contains unit tests for <see cref="AuthMessageMiddleware"/>, verifying that authorization bearer tokens
/// are attached exclusively to authorized base address endpoints and omitted for third-party URLs.
/// </summary>
public sealed class AuthMessageMiddlewareTests
{
    // A valid JWT format with exp in year 2286: {"alg":"HS256","typ":"JWT"}.{"exp":9999999999,"sub":"1"}.signature
    private const string VALID_FUTURE_JWT = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJleHAiOjk5OTk5OTk5OTksInN1YiI6IjEifQ.signature";

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static AuthSessionService CreateSessionService(AuthTokenStore tokenStore)
    {
        Mock<IHttpClientFactory> httpFactory = new();
        Mock<IConfiguration> config = new();
        Mock<ILogger<AuthSessionService>> logger = new();
        return new AuthSessionService(tokenStore, httpFactory.Object, config.Object, logger.Object);
    }

    private static Mock<ILocalStorageService> CreateMockLocalStorage()
    {
        Mock<ILocalStorageService> localStorage = new();
        localStorage.Setup(s => s.GetItemAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((key, _) =>
                ValueTask.FromResult<string?>(key == "hermes.auth.accessToken" ? VALID_FUTURE_JWT : "refresh-token-456"));
        localStorage.Setup(s => s.SetItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        return localStorage;
    }

    /// <summary>
    /// Tests that <see cref="AuthMessageMiddleware.IsAuthorizedEndpoint"/> returns true for relative URIs and matching hosts,
    /// but false for external origins.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/news", true)]
    [InlineData("http://localhost:5165/api/v1/news", true)]
    [InlineData("http://localhost:5165/other", true)]
    [InlineData("https://external-api.com/data", false)]
    [InlineData("http://evil-site.com/api", false)]
    public void IsAuthorizedEndpoint_Should_ValidateOriginsCorrectly(string url, bool expected)
    {
        // Arrange
        Uri baseAddress = new("http://localhost:5165");
        Uri requestUri = new(url, UriKind.RelativeOrAbsolute);

        // Act
        bool result = AuthMessageMiddleware.IsAuthorizedEndpoint(requestUri, baseAddress);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Tests that outgoing requests to authorized endpoints contain the Authorization Bearer header.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_AttachBearerToken_WhenDestinedForAuthorizedEndpoint()
    {
        // Arrange
        Mock<ILocalStorageService> localStorage = CreateMockLocalStorage();
        AuthTokenStore tokenStore = new(localStorage.Object);
        await tokenStore.PersistAsync(VALID_FUTURE_JWT, "refresh-token-456");

        AuthSessionService session = CreateSessionService(tokenStore);

        Uri baseAddress = new("http://localhost:5165");
        TestHttpMessageHandler innerHandler = new();
        AuthMessageMiddleware sut = new(tokenStore, session, baseAddress)
        {
            InnerHandler = innerHandler
        };

        using HttpClient client = new(sut) { BaseAddress = baseAddress };

        // Act
        await client.GetAsync("/api/v1/news/feed");

        // Assert
        Assert.NotNull(innerHandler.LastRequest);
        Assert.NotNull(innerHandler.LastRequest!.Headers.Authorization);
        Assert.Equal("Bearer", innerHandler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal(VALID_FUTURE_JWT, innerHandler.LastRequest.Headers.Authorization.Parameter);
    }

    /// <summary>
    /// Tests that outgoing requests to third-party endpoints DO NOT attach the Authorization Bearer header.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_NotAttachBearerToken_WhenDestinedForExternalEndpoint()
    {
        // Arrange
        Mock<ILocalStorageService> localStorage = CreateMockLocalStorage();
        AuthTokenStore tokenStore = new(localStorage.Object);
        await tokenStore.PersistAsync(VALID_FUTURE_JWT, "refresh-token-456");

        AuthSessionService session = CreateSessionService(tokenStore);

        Uri baseAddress = new("http://localhost:5165");
        TestHttpMessageHandler innerHandler = new();
        AuthMessageMiddleware sut = new(tokenStore, session, baseAddress)
        {
            InnerHandler = innerHandler
        };

        using HttpClient client = new(sut) { BaseAddress = baseAddress };

        // Act: Request to a third-party domain
        await client.GetAsync("https://cdn.external.com/avatar.png");

        // Assert: Authorization header MUST NOT be sent to third parties
        Assert.NotNull(innerHandler.LastRequest);
        Assert.Null(innerHandler.LastRequest!.Headers.Authorization);
    }
}
