using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Validation;

public static class ValidationResultExtensions
{
    public static ActionResult ToValidationProblem(this ValidationResult result, ControllerBase controller)
    {
        foreach (ValidationFailure validationError in result.Errors)
            controller.ModelState.AddModelError(validationError.PropertyName ?? string.Empty, validationError.ErrorMessage);

        return controller.ValidationProblem(controller.ModelState);
    }
}
