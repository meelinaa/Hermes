using Serilog.Context;

namespace Hermes.Api.Middleware;

/// <summary>
/// HTTP middleware that extracts or generates a unique CorrelationId for every incoming HTTP request and attaches it to response headers and Serilog logging contexts.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string CORRELATION_ID_HEADER_NAME = "X-Correlation-Id";
    public const string REQUEST_ID_HEADER_NAME = "X-Request-Id";
    public const string HTTP_CONTEXT_ITEM_KEY = "CorrelationId";

    /// <summary>
    /// Evaluates incoming headers for correlation identifiers, appends response headers, and pushes properties onto the Serilog LogContext.
    /// </summary>
    /// <param name="context">The HTTP request context.</param>
    /// <returns>A task representing the middleware invocation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        string? correlationId = context.Request.Headers[CORRELATION_ID_HEADER_NAME].FirstOrDefault()
            ?? context.Request.Headers[REQUEST_ID_HEADER_NAME].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        context.Items[HTTP_CONTEXT_ITEM_KEY] = correlationId;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CORRELATION_ID_HEADER_NAME))
                context.Response.Headers.Append(CORRELATION_ID_HEADER_NAME, correlationId);
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        {
            await next(context);
        }
    }
}
