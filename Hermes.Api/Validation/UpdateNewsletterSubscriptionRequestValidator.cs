using FluentValidation;
using Hermes.Application.Models.NewsletterSubscription;

namespace Hermes.Api.Validation;

/// <summary>
/// Validator for the UpdateNewsletterSubscriptionRequest DTO to ensure update parameters are valid.
/// </summary>
public sealed class UpdateNewsletterSubscriptionRequestValidator : AbstractValidator<UpdateNewsletterSubscriptionRequest>
{
    /// <summary>
    /// Initializes validation rules for newsletter subscription updates.
    /// </summary>
    public UpdateNewsletterSubscriptionRequestValidator()
    {
        RuleFor(request => request.Id)
            .GreaterThan(0).WithMessage("Subscription ID must be greater than zero.");
    }
}
