using FluentValidation;
using Hermes.Application.DTOs.NewsletterSubscription;

namespace Hermes.Api.Validators.Newsletter;

/// <summary>
/// Validator for <see cref="CreateNewsletterSubscriptionRequestDto"/> to ensure newsletter creation parameters are semantically valid and secure.
/// </summary>
public sealed class CreateNewsletterSubscriptionRequestValidator : AbstractValidator<CreateNewsletterSubscriptionRequestDto>
{
    /// <summary>
    /// Initializes validation rules for delivery schedule, search keywords, categories, languages, and country filters.
    /// Ensures that delivery days and times are specified and within valid domain bounds.
    /// </summary>
    public CreateNewsletterSubscriptionRequestValidator()
    {
        RuleFor(request => request.SendOnWeekdays)
            .NotEmpty().WithMessage("At least one weekday must be specified for digest delivery.");

        RuleForEach(request => request.SendOnWeekdays)
            .IsInEnum().WithMessage("Invalid weekday specified.");

        RuleFor(request => request.SendAtTimes)
            .NotEmpty().WithMessage("At least one delivery time must be specified for digest delivery.");

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
