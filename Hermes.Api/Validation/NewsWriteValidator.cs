using FluentValidation;
using Hermes.Domain.Entities;

namespace Hermes.Api.Validation;

/// <summary>Shared rules for POST/PUT bodies bound as <see cref="News"/>.</summary>
public sealed class NewsWriteValidator : AbstractValidator<News>
{
    /// <summary>Initializes validation rules for writing news entries.</summary>
    public NewsWriteValidator()
    {
        RuleFor(newsRequest => newsRequest.UserId).GreaterThan(0);
    }
}
