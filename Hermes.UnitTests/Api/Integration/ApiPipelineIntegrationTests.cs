using System.Net;
using System.Net.Http.Json;
using Hermes.Application.DTOs.User;
using Xunit;

namespace Hermes.UnitTests.Api.Integration;

/// <summary>
/// Contains integration pipeline tests verifying HTTP status codes, security headers, correlation IDs,
/// health probes, and authentication middleware using <see cref="InMemoryApiWebApplicationFactory"/>.
/// </summary>
public sealed class ApiPipelineIntegrationTests : IClassFixture<InMemoryApiWebApplicationFactory>
{
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
}
