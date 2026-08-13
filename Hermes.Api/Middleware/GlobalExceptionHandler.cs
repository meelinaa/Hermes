using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;

using Hermes.Domain.Constants;
using Hermes.Domain.Exceptions;

namespace Hermes.Api.Middleware;

/// <summary>
/// Global exception handler that catches unhandled exceptions and maps them to appropriate RFC 7807 ProblemDetails responses.
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        httpContext.Response.ContentType = "application/problem+json";

        if (exception is EmailAlreadyExistsException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = CreateMinimalProblem("Email already exists.", StatusCodes.Status409Conflict)
            });
            return true;
        }

        if (exception is UserNotFoundException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = CreateMinimalProblem("User not found.", StatusCodes.Status404NotFound)
            });
            return true;
        }

        if (exception is EmailNotVerifiedException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = CreateMinimalProblem("Email not verified.", StatusCodes.Status403Forbidden)
            });
            return true;
        }

        if (exception is NewsletterSubscriptionNotFoundException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = CreateMinimalProblem("Newsletter subscription not found.", StatusCodes.Status404NotFound)
            });
            return true;
        }

        if (exception is NewsletterSubscriptionAccessDeniedException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = CreateMinimalProblem("Newsletter subscription access denied.", StatusCodes.Status403Forbidden)
            });
            return true;
        }

        if (exception is WrongCurrentPasswordException wcp)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Type = HermesProblemTypeConstants.WRONG_CURRENT_PASSWORD,
                    Title = "Invalid current password",
                    Detail = wcp.Message,
                    Status = StatusCodes.Status400BadRequest
                }
            });
            return true;
        }

        if (exception is DomainValidationException validationException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = CreateMinimalProblem(validationException.Message, StatusCodes.Status400BadRequest)
            });
            return true;
        }

        if (exception is DomainException domainException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = CreateMinimalProblem(domainException.Message, StatusCodes.Status400BadRequest)
            });
            return true;
        }

        if (exception is VerificationCodeMismatchException vcm)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = CreateMinimalProblem(vcm.Message, StatusCodes.Status400BadRequest)
            });
            return true;
        }

        Log.Error(exception, "Unhandled exception");
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = CreateMinimalProblem("An error occurred.", StatusCodes.Status500InternalServerError)
        });

        return true;
    }

    /// <summary>
    /// Constructs a minimal <see cref="ProblemDetails"/> instance with specified title and status code.
    /// </summary>
    private static ProblemDetails CreateMinimalProblem(string title, int status) => new()
    {
        Title = title,
        Status = status,
        Detail = null,
        Type = null,
        Instance = null
    };
}
