using Hermes.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Http;

/// <summary>
/// Extension methods for <see cref="ControllerBase"/> to produce standardized RFC 7231 Problem Details responses.
/// </summary>
public static class ApiProblemResultExtensions
{
    private const string RFC_7231 = "https://tools.ietf.org/html/rfc7231";

    /// <summary>
    /// Generates an HTTP 400 Bad Request ProblemDetails response with specified detail message.
    /// </summary>
    /// <param name="controller">The controller instance.</param>
    /// <param name="detail">Detailed description of the validation or request error.</param>
    /// <returns>An <see cref="ActionResult"/> configured as HTTP 400 ProblemDetails.</returns>
    public static ActionResult BadRequestProblem(this ControllerBase controller, string detail) =>
        controller.Problem(
            title: "Bad Request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            type: $"{RFC_7231}#section-6.5.1");

    /// <summary>
    /// Generates an HTTP 404 Not Found ProblemDetails response.
    /// </summary>
    /// <param name="controller">The controller instance.</param>
    /// <param name="detail">Optional detailed explanation of the missing resource.</param>
    /// <returns>An <see cref="ActionResult"/> configured as HTTP 404 ProblemDetails.</returns>
    public static ActionResult NotFoundProblem(this ControllerBase controller, string? detail = null) =>
        controller.Problem(
            title: "Not Found",
            detail: detail,
            statusCode: StatusCodes.Status404NotFound,
            type: $"{RFC_7231}#section-6.5.4");

    /// <summary>
    /// Generates an HTTP 401 Unauthorized ProblemDetails response.
    /// </summary>
    /// <param name="controller">The controller instance.</param>
    /// <param name="detail">Optional detailed explanation of authentication failure.</param>
    /// <returns>An <see cref="ActionResult"/> configured as HTTP 401 ProblemDetails.</returns>
    public static ActionResult UnauthorizedProblem(this ControllerBase controller, string? detail = null) =>
        controller.Problem(
            title: "Unauthorized",
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized,
            type: $"{RFC_7231}#section-6.5.2");

    /// <summary>
    /// Generates an HTTP 403 Forbidden ProblemDetails response.
    /// </summary>
    /// <param name="controller">The controller instance.</param>
    /// <param name="detail">Optional detailed explanation of authorization refusal.</param>
    /// <returns>An <see cref="ActionResult"/> configured as HTTP 403 ProblemDetails.</returns>
    public static ActionResult ForbiddenProblem(this ControllerBase controller, string? detail = null) =>
        controller.Problem(
            title: "Forbidden",
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden,
            type: $"{RFC_7231}#section-6.5.3");

    /// <summary>
    /// Generates a domain-specific HTTP 400 Bad Request ProblemDetails response indicating invalid current password verification.
    /// </summary>
    /// <param name="controller">The controller instance.</param>
    /// <param name="detail">Detailed description of the password validation failure.</param>
    /// <returns>An <see cref="ActionResult"/> configured with custom Hermes problem type for wrong current password.</returns>
    public static ActionResult WrongCurrentPasswordProblem(this ControllerBase controller, string detail) =>
        controller.Problem(
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid current password",
            type: HermesProblemTypeConstants.WRONG_CURRENT_PASSWORD);
}
