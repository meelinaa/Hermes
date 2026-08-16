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

        When(request => request.SendOnWeekdays is { Count: > 0 }, () =>
        {
            RuleForEach(request => request.SendOnWeekdays)
                .IsInEnum().WithMessage("Invalid weekday specified.");
        });

        When(request => request.Keywords is not null, () =>
        {
            RuleFor(request => request.Keywords)
                .Must(k => k!.Count <= 20).WithMessage("At most 20 keywords may be specified.");

            RuleForEach(request => request.Keywords)
                .NotEmpty().WithMessage("Keywords cannot be empty.")
                .MaximumLength(100).WithMessage("Individual keywords cannot exceed 100 characters.");
        });

        When(request => request.Category is not null, () =>
        {
            RuleForEach(request => request.Category)
                .IsInEnum().WithMessage("Invalid news category specified.");
        });

        When(request => request.Languages is not null, () =>
        {
            RuleForEach(request => request.Languages)
                .IsInEnum().WithMessage("Invalid language specified.");
        });

        When(request => request.Countries is not null, () =>
        {
            RuleForEach(request => request.Countries)
                .IsInEnum().WithMessage("Invalid country specified.");
        });
    }
}
