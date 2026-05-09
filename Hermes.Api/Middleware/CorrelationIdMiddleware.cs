using Serilog.Context;

namespace Hermes.Api.Middleware;

/// <summary>
/// Adds a correlation ID to each request: reads from header (X-Correlation-Id or X-Request-Id) or generates one.
/// Puts it in <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> and response header, and enriches Serilog logs for the request scope.
/// </summary>
/// <remarks>Creates the middleware with the next delegate in the pipeline.</remarks>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string CORRELATION_ID_HEADER_NAME = "X-Correlation-Id";
    public const string REQUEST_ID_HEADER_NAME = "X-Request-Id";
    public const string HTTP_CONTEXT_ITEM_KEY = "CorrelationId";

    /// <summary>Resolves or generates the correlation ID, adds it to context and response header, and enriches Serilog for the request scope.</summary>
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
