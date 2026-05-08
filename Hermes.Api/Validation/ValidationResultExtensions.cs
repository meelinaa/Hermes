using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Validation;

/// <summary>Maps FluentValidation results to RFC 7807 <see cref="ValidationProblemDetails"/> (400).</summary>
public static class ValidationResultExtensions
{
    /// <summary>Converts a FluentValidation result into an RFC 7807 validation problem response.</summary>
    public static ActionResult ToValidationProblem(this ValidationResult result, ControllerBase controller)
    {
        foreach (ValidationFailure validationError in result.Errors)
            controller.ModelState.AddModelError(validationError.PropertyName ?? string.Empty, validationError.ErrorMessage);

        return controller.ValidationProblem(controller.ModelState);
    }
}
