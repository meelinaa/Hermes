using Serilog.Context;

namespace Hermes.Api.Middleware;

/// <summary>
/// Adds a correlation ID to each request: reads from header (X-Correlation-Id or X-Request-Id) or generates one.
/// Puts it in <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> and response header, and enriches Serilog logs for the request scope.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";
    public const string RequestIdHeaderName = "X-Request-Id";
    public const string HttpContextItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware with the next delegate in the pipeline.</summary>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Resolves or generates the correlation ID, adds it to context and response header, and enriches Serilog for the request scope.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault()
            ?? context.Request.Headers[RequestIdHeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        context.Items[HttpContextItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
                context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        {
            await _next(context);
        }
    }
}