using Hermes.Api.Middleware;
using Hermes.Domain;
using Hermes.Domain.Exceptions;
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
using System.Text.Json;

namespace Hermes.Api.Hosting;

public static class ApiApplicationPipelineExtensions
{
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

        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                IProblemDetailsService problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();
                IExceptionHandlerFeature? exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
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
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await problemDetailsService.WriteAsync(new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = new ProblemDetails
                        {
                            Type = HermesProblemTypes.WRONG_CURRENT_PASSWORD,
                            Title = "Aktuelles Passwort ungültig",
                            Detail = wcp.Message,
                            Status = StatusCodes.Status400BadRequest
                        }
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

    private static ProblemDetails CreateMinimalProblem(string title, int status) => new()
    {
        Title = title,
        Status = status,
        Detail = null,
        Type = null,
        Instance = null
    };
}
