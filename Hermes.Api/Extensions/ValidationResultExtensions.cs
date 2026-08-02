using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Extensions;

/// <summary>
/// Extension methods for mapping FluentValidation <see cref="ValidationResult"/> instances into ASP.NET Core MVC problem details responses.
/// </summary>
public static class ValidationResultExtensions
{
    /// <summary>
    /// Converts a FluentValidation <see cref="ValidationResult"/> into an ASP.NET Core <see cref="ActionResult"/> validation problem.
    /// </summary>
    /// <param name="result">The validation result containing failure details.</param>
    /// <param name="controller">The controller instance executing the request.</param>
    /// <returns>An <see cref="ActionResult"/> containing structured validation problem details.</returns>
    public static ActionResult ToValidationProblem(this ValidationResult result, ControllerBase controller)
    {
        foreach (ValidationFailure validationError in result.Errors)
            controller.ModelState.AddModelError(validationError.PropertyName ?? string.Empty, validationError.ErrorMessage);

        return controller.ValidationProblem(controller.ModelState);
    }
}
