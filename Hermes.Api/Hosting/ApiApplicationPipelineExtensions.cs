using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Serilog;

using Hermes.Api.Middleware;
using Hermes.Api.Options;
using Hermes.Domain.Constants;
using Hermes.Domain.Exceptions;

namespace Hermes.Api.Hosting;

/// <summary>
/// Extension methods for configuring the ASP.NET Core HTTP request processing pipeline for Hermes API.
/// </summary>
public static class ApiApplicationPipelineExtensions
{
    /// <summary>
    /// Configures the HTTP request pipeline including correlation middleware, exception handling, rate limiting, authentication, and endpoint mapping.
    /// </summary>
    /// <param name="app">The WebApplication host instance.</param>
    public static void UseHermesApiPipeline(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();

        app.UseRequestTimeouts();

        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                string? correlationId = httpContext.Items[CorrelationIdMiddleware.HTTP_CONTEXT_ITEM_KEY]?.ToString();
                if (!string.IsNullOrEmpty(correlationId))
                    diagnosticContext.Set("CorrelationId", correlationId);
            };
        });

        app.UseExceptionHandler();

        HermesOpenApiOptions openApiOpts = app.Services.GetRequiredService<IOptions<HermesOpenApiOptions>>().Value;
        bool exposeOpenApi = openApiOpts.MapInProduction || app.Environment.IsProduction() is false;

        if (exposeOpenApi &&
            app.Environment.IsProduction() &&
            !string.IsNullOrWhiteSpace(openApiOpts.DocumentationApiKey))
        {
            string expectedKey = openApiOpts.DocumentationApiKey;
            string keyHeader = openApiOpts.DocumentationApiKeyHeader;
            PathString docsPrefix = new(openApiOpts.DocumentationPathPrefix);
            app.UseWhen(
                ctx => ctx.Request.Path.StartsWithSegments(docsPrefix),
                branch => branch.Use(async (HttpContext ctx, RequestDelegate next) =>
                {
                    if (!ctx.Request.Headers.TryGetValue(keyHeader, out StringValues supplied) ||
                        supplied.Count == 0 ||
                        !string.Equals(supplied.ToString(), expectedKey, StringComparison.Ordinal))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }

                    await next(ctx);
                }));
        }

        if (exposeOpenApi)
            app.MapOpenApi(openApiOpts.RoutePattern);

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseCors("FrontendPolicy");

        app.UseRateLimiter();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseStatusCodePages();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = static async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    Status = report.Status.ToString(),
                    Checks = report.Entries.Select(entry => new
                    {
                        Component = entry.Key,
                        Status = entry.Value.Status.ToString(),
                        entry.Value.Description
                    }),
                    Duration = report.TotalDuration
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
            }
        });

        app.MapControllers();
    }

}
