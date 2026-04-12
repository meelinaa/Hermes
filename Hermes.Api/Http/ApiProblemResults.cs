using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Http;

/// <summary>RFC 7807 <c>ProblemDetails</c> helpers for consistent JSON errors (400/401/403/404).</summary>
public static class ApiProblemResults
{
    // rfc7231 is the base for HTTP status codes and their semantics, including the "type" URI references for problem details.
    private const string Rfc7231 = "https://tools.ietf.org/html/rfc7231";

    public static ActionResult BadRequestProblem(this ControllerBase controller, string detail) =>
        controller.Problem(
            title: "Bad Request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            type: $"{Rfc7231}#section-6.5.1");

    public static ActionResult NotFoundProblem(this ControllerBase controller, string? detail = null) =>
        controller.Problem(
            title: "Not Found",
            detail: detail,
            statusCode: StatusCodes.Status404NotFound,
            type: $"{Rfc7231}#section-6.5.4");

    public static ActionResult UnauthorizedProblem(this ControllerBase controller, string? detail = null) =>
        controller.Problem(
            title: "Unauthorized",
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized,
            type: $"{Rfc7231}#section-6.5.2");

    public static ActionResult ForbiddenProblem(this ControllerBase controller, string? detail = null) =>
        controller.Problem(
            title: "Forbidden",
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden,
            type: $"{Rfc7231}#section-6.5.3");
}
