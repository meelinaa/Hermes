using Hermes.Api.Middleware;
using Hermes.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Text.Json;

namespace Hermes.Api.Hosting;

/// <summary>
/// Configures the HTTP request pipeline: correlation IDs, timeouts, logging, exception handling, CORS, authorization, and health endpoints.
/// </summary>
public static class ApiApplicationPipelineExtensions
{
    /// <summary>
    /// Registers middleware and endpoints for the Hermes REST API. Order matters (first registered = outermost for incoming requests).
    /// </summary>
    public static void UseHermesApiPipeline(this WebApplication app)
    {
        // Propagate or generate X-Correlation-ID for distributed tracing in logs.
        app.UseMiddleware<CorrelationIdMiddleware>();

        // Enforce request timeout policies registered in DI.
        app.UseRequestTimeouts();

        // One-line HTTP request logs with optional CorrelationId enrichment.
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextItemKey]?.ToString();
                if (!string.IsNullOrEmpty(correlationId))
                    diagnosticContext.Set("CorrelationId", correlationId);
            };
        });

        // Map unhandled exceptions to ProblemDetails (JSON); hides exception message in non-development environments.
        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
                var exceptionHandlerFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                if (exceptionHandlerFeature?.Error is { } error)
                {
                    context.Response.ContentType = "application/problem+json";

                    if (error is EmailAlreadyExistsException duplicateEmail)
                    {
                        context.Response.StatusCode = StatusCodes.Status409Conflict;
                        await problemDetailsService.WriteAsync(new ProblemDetailsContext
                        {
                            HttpContext = context,
                            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                            {
                                Title = "E-Mail already exists.",
                                Status = StatusCodes.Status409Conflict,
                                Detail = duplicateEmail.Message,
                                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                                Instance = $"{context.Request.Method} {context.Request.Path}"
                            }
                        });
                        return;
                    }

                    if (error is UserNotFoundException userNotFound)
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await problemDetailsService.WriteAsync(new ProblemDetailsContext
                        {
                            HttpContext = context,
                            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                            {
                                Title = "User not found.",
                                Status = StatusCodes.Status404NotFound,
                                Detail = userNotFound.Message,
                                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                                Instance = $"{context.Request.Method} {context.Request.Path}"
                            }
                        });
                        return;
                    }

                    if (error is EmailNotVerifiedException emailNotVerified)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await problemDetailsService.WriteAsync(new ProblemDetailsContext
                        {
                            HttpContext = context,
                            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                            {
                                Title = "E-mail not verified.",
                                Status = StatusCodes.Status403Forbidden,
                                Detail = emailNotVerified.Message,
                                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                                Instance = $"{context.Request.Method} {context.Request.Path}"
                            }
                        });
                        return;
                    }

                    if (error is NewsNotFoundException newsNotFound)
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await problemDetailsService.WriteAsync(new ProblemDetailsContext
                        {
                            HttpContext = context,
                            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                            {
                                Title = "News not found.",
                                Status = StatusCodes.Status404NotFound,
                                Detail = newsNotFound.Message,
                                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                                Instance = $"{context.Request.Method} {context.Request.Path}"
                            }
                        });
                        return;
                    }

                    if (error is NewsAccessDeniedException newsAccessDenied)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await problemDetailsService.WriteAsync(new ProblemDetailsContext
                        {
                            HttpContext = context,
                            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                            {
                                Title = "News access denied.",
                                Status = StatusCodes.Status403Forbidden,
                                Detail = newsAccessDenied.Message,
                                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                                Instance = $"{context.Request.Method} {context.Request.Path}"
                            }
                        });
                        return;
                    }

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    await problemDetailsService.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                        {
                            Title = "An error occurred",
                            Status = StatusCodes.Status500InternalServerError,
                            Detail = app.Environment.IsDevelopment() ? error.Message : null,
                            Instance = $"{context.Request.Method} {context.Request.Path}"
                        }
                    });
                }
            });
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.MapOpenApi();
            // Permissive CORS for local frontend tooling; production uses FrontendPolicy below.
            app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        }
        else
        {
            app.UseHttpsRedirection();
            app.UseCors("FrontendPolicy");
        }

        app.UseAuthorization();

        // Return a small HTML or plain body for 404/405 etc. instead of an empty response body.
        app.UseStatusCodePages();

        // Liveness: always 200 (no DB check) — used by orchestrators that only need process-up.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        // Readiness: runs checks tagged "ready" (database).
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    Status = report.Status.ToString(),
                    Checks = report.Entries.Select(e => new
                    {
                        Component = e.Key,
                        Status = e.Value.Status.ToString(),
                        Description = e.Value.Description
                    }),
                    Duration = report.TotalDuration
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
            }
        });

        app.MapControllers();
    }
}