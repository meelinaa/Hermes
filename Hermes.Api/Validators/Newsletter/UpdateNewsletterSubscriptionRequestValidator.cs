using FluentValidation;
using Hermes.Application.DTOs.NewsletterSubscription;

namespace Hermes.Api.Validators.Newsletter;

/// <summary>
/// Validator for the UpdateNewsletterSubscriptionRequestDto DTO to ensure update parameters are valid.
/// </summary>
public sealed class UpdateNewsletterSubscriptionRequestValidator : AbstractValidator<UpdateNewsletterSubscriptionRequestDto>
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
