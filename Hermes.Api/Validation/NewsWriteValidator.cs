using FluentValidation;
using Hermes.Domain.Entities;

namespace Hermes.Api.Validation;

/// <summary>Shared rules for POST/PUT bodies bound as <see cref="News"/>.</summary>
public sealed class NewsWriteValidator : AbstractValidator<News>
{
    public NewsWriteValidator()
    {
        RuleFor(n => n.UserId).GreaterThan(0);
    }
}
