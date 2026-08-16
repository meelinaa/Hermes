using Hermes.Api.Middleware;
using Hermes.Domain.Constants;
using Hermes.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Middleware;

/// <summary>
/// Contains unit tests for <see cref="GlobalExceptionHandler"/>,
/// verifying RFC 7807 problem details status mapping across domain and unhandled exceptions.
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    private sealed class ConcreteDomainException(string message) : DomainException(message);

    private static (GlobalExceptionHandler handler, DefaultHttpContext httpContext, Mock<IProblemDetailsService> problemDetailsMock, ProblemDetailsContext? captured) CreateSut()
    {
        Mock<IProblemDetailsService> problemDetailsMock = new();
        ProblemDetailsContext? captured = null;
        problemDetailsMock.Setup(p => p.WriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(ctx => captured = ctx)
            .Returns(ValueTask.CompletedTask);

        DefaultHttpContext httpContext = new();
        GlobalExceptionHandler handler = new(problemDetailsMock.Object);

        return (handler, httpContext, problemDetailsMock, captured);
    }

    /// <summary>
    /// Tests that <see cref="EmailAlreadyExistsException"/> maps to HTTP 409 Conflict.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Should_MapEmailAlreadyExists_ToConflict409()
    {
        // Arrange
        var (handler, httpContext, _, _) = CreateSut();
        EmailAlreadyExistsException ex = new("duplicate@hermes.de");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UserNotFoundException"/> maps to HTTP 404 NotFound.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Should_MapUserNotFound_ToNotFound404()
    {
        // Arrange
        var (handler, httpContext, _, _) = CreateSut();
        UserNotFoundException ex = new("User missing");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="EmailNotVerifiedException"/> maps to HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Should_MapEmailNotVerified_ToForbidden403()
    {
        // Arrange
        var (handler, httpContext, _, _) = CreateSut();
        EmailNotVerifiedException ex = new("unverified@hermes.de");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscriptionNotFoundException"/> maps to HTTP 404 NotFound.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Should_MapNewsletterNotFound_ToNotFound404()
    {
        // Arrange
        var (handler, httpContext, _, _) = CreateSut();
        NewsletterSubscriptionNotFoundException ex = new("News missing");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterSubscriptionAccessDeniedException"/> maps to HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Should_MapNewsletterAccessDenied_ToForbidden403()
    {
        // Arrange
        var (handler, httpContext, _, _) = CreateSut();
        NewsletterSubscriptionAccessDeniedException ex = new("Access denied");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="WrongCurrentPasswordException"/> maps to HTTP 400 BadRequest with custom problem type.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Should_MapWrongCurrentPassword_ToBadRequest400WithCustomType()
    {
        // Arrange
        var (handler, httpContext, _, _) = CreateSut();
        WrongCurrentPasswordException ex = new();

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="DomainValidationException"/> maps to HTTP 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Should_MapDomainValidationException_ToBadRequest400()
    {
        // Arrange
        var (handler, httpContext, _, _) = CreateSut();
        DomainValidationException ex = new("Invalid domain invariant");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Tests that general <see cref="DomainException"/> maps to HTTP 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Should_MapDomainException_ToBadRequest400()
    {
        // Arrange
        var (handler, httpContext, _, _) = CreateSut();
        ConcreteDomainException ex = new("General domain failure");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="VerificationCodeMismatchException"/> maps to HTTP 400 BadRequest.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Should_MapVerificationCodeMismatch_ToBadRequest400()
    {
        // Arrange
        var (handler, httpContext, _, _) = CreateSut();
        VerificationCodeMismatchException ex = new("Code does not match");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Tests that unhandled general exceptions map to HTTP 500 InternalServerError.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Should_MapUnhandledException_ToInternalServerError500()
    {
        // Arrange
        var (handler, httpContext, _, _) = CreateSut();
        InvalidOperationException ex = new("Unexpected database crash");

        // Act
        bool handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
    }
}
