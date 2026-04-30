using Hermes.Api.Middleware;
using Hermes.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
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

        // JSON ProblemDetails for all environments. Do not register UseDeveloperExceptionPage here — it runs inside
        // the exception handler pipeline and would return verbose 500 HTML/JSON before domain exceptions map to 4xx.
        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
                var exceptionHandlerFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                if (exceptionHandlerFeature?.Error is not { } error)
                    return;

                context.Response.ContentType = "application/problem+json";

                if (error is EmailAlreadyExistsException)
                {
                    context.Response.StatusCode = StatusCodes.Status409Conflict;
                    await problemDetailsService.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = CreateMinimalProblem("Email already exists.", StatusCodes.Status409Conflict)
                    });
                    return;
                }

                if (error is UserNotFoundException)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await problemDetailsService.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = CreateMinimalProblem("User not found.", StatusCodes.Status404NotFound)
                    });
                    return;
                }

                if (error is EmailNotVerifiedException)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await problemDetailsService.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = CreateMinimalProblem("Email not verified.", StatusCodes.Status403Forbidden)
                    });
                    return;
                }

                if (error is NewsNotFoundException)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await problemDetailsService.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = CreateMinimalProblem("News not found.", StatusCodes.Status404NotFound)
                    });
                    return;
                }

                if (error is NewsAccessDeniedException)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await problemDetailsService.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = CreateMinimalProblem("News access denied.", StatusCodes.Status403Forbidden)
                    });
                    return;
                }

                if (error is WrongCurrentPasswordException wcp)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await problemDetailsService.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = CreateMinimalProblem(wcp.Message, StatusCodes.Status401Unauthorized)
                    });
                    return;
                }

                if (error is VerificationCodeMismatchException vcm)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await problemDetailsService.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = CreateMinimalProblem(vcm.Message, StatusCodes.Status400BadRequest)
                    });
                    return;
                }

                Log.Error(error, "Unhandled exception");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = CreateMinimalProblem("An error occurred.", StatusCodes.Status500InternalServerError)
                });
            });
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }
        else
        {
            app.UseHttpsRedirection();
        }

        // Origins from configuration (Cors:AllowedOrigins), e.g. Hermes.WebFrontend (Blazor) URLs.
        app.UseCors("FrontendPolicy");

        // Validates JWT on incoming requests (Bearer) before authorization policies run.
        app.UseAuthentication();
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

    /// <summary>Short RFC 7807 body: title + status only (no exception message, type, or instance).</summary>
    private static ProblemDetails CreateMinimalProblem(string title, int status) => new()
    {
        Title = title,
        Status = status,
        Detail = null,
        Type = null,
        Instance = null
    };
}