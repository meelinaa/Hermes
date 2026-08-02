using Hermes.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Http;

public static class ApiProblemResultExtensions
{
    private const string RFC_7231 = "https://tools.ietf.org/html/rfc7231";

    public static ActionResult BadRequestProblem(this ControllerBase controller, string detail) =>
        controller.Problem(
            title: "Bad Request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            type: $"{RFC_7231}#section-6.5.1");

    public static ActionResult NotFoundProblem(this ControllerBase controller, string? detail = null) =>
        controller.Problem(
            title: "Not Found",
            detail: detail,
            statusCode: StatusCodes.Status404NotFound,
            type: $"{RFC_7231}#section-6.5.4");

    public static ActionResult UnauthorizedProblem(this ControllerBase controller, string? detail = null) =>
        controller.Problem(
            title: "Unauthorized",
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized,
            type: $"{RFC_7231}#section-6.5.2");

    public static ActionResult ForbiddenProblem(this ControllerBase controller, string? detail = null) =>
        controller.Problem(
            title: "Forbidden",
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden,
            type: $"{RFC_7231}#section-6.5.3");

    public static ActionResult WrongCurrentPasswordProblem(this ControllerBase controller, string detail) =>
        controller.Problem(
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Aktuelles Passwort ungültig",
            type: HermesProblemTypeConstants.WRONG_CURRENT_PASSWORD);
}
