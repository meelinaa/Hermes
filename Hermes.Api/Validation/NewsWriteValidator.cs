using FluentValidation;
using Hermes.Domain.Entities;

namespace Hermes.Api.Validation;

/// <summary>Shared rules for POST/PUT bodies bound as <see cref="News"/>.</summary>
public sealed class NewsWriteValidator : AbstractValidator<News>
{
    public NewsWriteValidator()
    {
        RuleFor(newsRequest => newsRequest.UserId).GreaterThan(0);
    }
}
