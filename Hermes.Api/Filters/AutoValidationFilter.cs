using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hermes.Api.Filters;

/// <summary>
/// Global action filter that automatically performs validation using FluentValidation
/// for all action parameters that have a registered validator.
/// If validation fails, it short-circuits the request with a 400 Bad Request ProblemDetails.
/// </summary>
public sealed class AutoValidationFilter : IAsyncActionFilter
{
    /// <summary>
    /// Executes validation logic automatically before the controller action is invoked.
    /// It queries DI for validators of each parameter, validates them, and builds
    /// a validation problem response if any validation failures are detected.
    /// </summary>
    /// <param name="context">The action execution context containing request parameters.</param>
    /// <param name="next">The delegate to execute the next action filter or the action itself.</param>
    /// <returns>A task representing the asynchronous filter execution.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            var validator = context.HttpContext.RequestServices.GetService(validatorType) as IValidator;

            if (validator is not null)
            {
                var validationContext = new ValidationContext<object>(argument);
                var validationResult = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted).ConfigureAwait(false);

                if (!validationResult.IsValid)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        context.ModelState.AddModelError(error.PropertyName ?? string.Empty, error.ErrorMessage);
                    }
                }
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
            return;
        }

        await next();
    }
}
