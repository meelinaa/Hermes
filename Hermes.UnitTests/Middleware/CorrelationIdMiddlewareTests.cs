using Hermes.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Hermes.UnitTests.Middleware;

/// <summary>
/// Contains unit tests for <see cref="CorrelationIdMiddleware"/> and <see cref="SecurityHeadersMiddleware"/>,
/// testing correlation tracking and HTTP security header injections.
/// </summary>
public sealed class CorrelationIdMiddlewareTests
{
    /// <summary>
    /// Tests that existing X-Correlation-Id headers in incoming requests are preserved and attached to context items.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_Should_UseExistingCorrelationIdHeader_WhenPresent()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Headers[CorrelationIdMiddleware.CORRELATION_ID_HEADER_NAME] = "custom-corr-id-123";

        bool nextInvoked = false;
        CorrelationIdMiddleware middleware = new(ctx =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextInvoked);
        Assert.Equal("custom-corr-id-123", context.Items[CorrelationIdMiddleware.HTTP_CONTEXT_ITEM_KEY]);
    }

    /// <summary>
    /// Tests that X-Request-Id is used as fallback when X-Correlation-Id is missing.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_Should_UseRequestIdHeader_WhenCorrelationIdMissing()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Headers[CorrelationIdMiddleware.REQUEST_ID_HEADER_NAME] = "req-id-789";

        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("req-id-789", context.Items[CorrelationIdMiddleware.HTTP_CONTEXT_ITEM_KEY]);
    }

    /// <summary>
    /// Tests that a new GUID correlation ID is generated when no headers are supplied.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_Should_GenerateNewGuid_WhenNoHeadersPresent()
    {
        // Arrange
        DefaultHttpContext context = new();
        CorrelationIdMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        object? generated = context.Items[CorrelationIdMiddleware.HTTP_CONTEXT_ITEM_KEY];
        Assert.NotNull(generated);
        Assert.True(Guid.TryParse(generated.ToString(), out _));
    }
}

/// <summary>
/// Contains unit tests for <see cref="SecurityHeadersMiddleware"/>.
/// </summary>
public sealed class SecurityHeadersMiddlewareTests
{
    /// <summary>
    /// Tests that security headers (nosniff, DENY, CSP, etc.) are injected into all HTTP responses.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_Should_AttachSecurityHeaders()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.IsHttps = false;

        SecurityHeadersMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
        Assert.Equal("0", context.Response.Headers["X-XSS-Protection"]);
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    /// <summary>
    /// Tests that Strict-Transport-Security header is attached when the request is served over HTTPS.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_Should_AttachHstsHeader_WhenRequestIsHttps()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.IsHttps = true;

        SecurityHeadersMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
        Assert.Equal("max-age=31536000; includeSubDomains", context.Response.Headers["Strict-Transport-Security"]);
    }
}
