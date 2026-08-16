using Hermes.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Hermes.UnitTests.Security;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Injects_All_Standard_Security_Headers()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.IsHttps = false;

        var middleware = new SecurityHeadersMiddleware(innerContext => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        IHeaderDictionary headers = context.Response.Headers;
        Assert.Equal("nosniff", headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", headers["X-Frame-Options"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"].ToString());
        Assert.Equal("0", headers["X-XSS-Protection"].ToString());
        Assert.Contains("default-src 'self'", headers["Content-Security-Policy"].ToString());
        Assert.Contains("camera=()", headers["Permissions-Policy"].ToString());
        Assert.False(headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task InvokeAsync_Injects_Hsts_Header_When_Request_Is_Https()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.IsHttps = true;

        var middleware = new SecurityHeadersMiddleware(innerContext => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        IHeaderDictionary headers = context.Response.Headers;
        Assert.True(headers.ContainsKey("Strict-Transport-Security"));
        Assert.Contains("max-age=31536000", headers["Strict-Transport-Security"].ToString());
    }
}
