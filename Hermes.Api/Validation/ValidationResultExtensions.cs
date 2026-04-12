using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Validation;

/// <summary>Maps FluentValidation results to RFC 7807 <see cref="ValidationProblemDetails"/> (400).</summary>
public static class ValidationResultExtensions
{
    public static ActionResult ToValidationProblem(this ValidationResult result, ControllerBase controller)
    {
        foreach (var e in result.Errors)
            controller.ModelState.AddModelError(e.PropertyName ?? string.Empty, e.ErrorMessage);

        return controller.ValidationProblem(controller.ModelState);
    }
}
