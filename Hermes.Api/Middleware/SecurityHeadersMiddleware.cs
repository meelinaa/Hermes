using Microsoft.AspNetCore.Http;

namespace Hermes.Api.Middleware;

/// <summary>
/// Middleware that automatically attaches hardened HTTP security headers to all outgoing API and static responses.
/// Mitigates cross-site scripting (XSS), clickjacking, MIME-sniffing, and protocol downgrade attacks.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string CSP_POLICY = "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self';";
    private const string PERMISSIONS_POLICY = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
    private const string HSTS_POLICY = "max-age=31536000; includeSubDomains";

    /// <summary>
    /// Injects industry-standard security headers into the response headers collection before invoking the next middleware.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous request processing.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IHeaderDictionary headers = context.Response.Headers;

        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        headers.TryAdd("X-XSS-Protection", "0");
        headers.TryAdd("Permissions-Policy", PERMISSIONS_POLICY);
        headers.TryAdd("Content-Security-Policy", CSP_POLICY);

        if (context.Request.IsHttps)
        {
            headers.TryAdd("Strict-Transport-Security", HSTS_POLICY);
        }

        await next(context).ConfigureAwait(false);
    }
}
