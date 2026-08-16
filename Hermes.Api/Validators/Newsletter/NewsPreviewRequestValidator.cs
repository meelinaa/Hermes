using FluentValidation;
using Hermes.Application.DTOs.NewsArticle;

namespace Hermes.Api.Validators.Newsletter;

/// <summary>
/// Validator for <see cref="NewsPreviewRequestDto"/> ensuring live feed preview filter queries conform to bounds.
/// </summary>
public sealed class NewsPreviewRequestValidator : AbstractValidator<NewsPreviewRequestDto>
{
    /// <summary>
    /// Initializes validation rules for keywords length and valid enumeration values for categories, languages, and countries.
    /// Protects downstream external providers from excessive query strings.
    /// </summary>
    public NewsPreviewRequestValidator()
    {
        When(request => !string.IsNullOrEmpty(request.Keywords), () =>
        {
            RuleFor(request => request.Keywords)
                .MaximumLength(200).WithMessage("Keywords query cannot exceed 200 characters.");
        });

        When(request => request.Categories is not null, () =>
        {
            RuleForEach(request => request.Categories)
                .IsInEnum().WithMessage("Invalid category specified.");
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
