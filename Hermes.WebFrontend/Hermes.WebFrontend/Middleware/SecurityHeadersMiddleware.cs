using Microsoft.AspNetCore.Http;

namespace Hermes.WebFrontend.Middleware;

/// <summary>
/// Middleware that attaches hardened HTTP security headers and Content-Security-Policy (CSP) tailored for Blazor WebAssembly applications.
/// Mitigates XSS, clickjacking, MIME-sniffing, and data exfiltration while permitting WebAssembly execution and dynamic stylesheet evaluation.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string CSP_POLICY =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-eval' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' http://localhost:* https://localhost:* ws://localhost:* wss://localhost:*; " +
        "frame-ancestors 'none'; " +
        "object-src 'none'; " +
        "base-uri 'self';";

    private const string PERMISSIONS_POLICY = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";
    private const string HSTS_POLICY = "max-age=31536000; includeSubDomains";

    /// <summary>
    /// Attaches security headers to all outgoing responses.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A Task representing the asynchronous middleware execution.</returns>
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
