using FluentValidation;
using Hermes.Application.Models.News;

namespace Hermes.Api.Validation;

/// <summary>Rules for <see cref="UpdateNewsRequest"/>.</summary>
public sealed class UpdateNewsRequestValidator : AbstractValidator<UpdateNewsRequest>
{
    public UpdateNewsRequestValidator()
    {
        RuleFor(request => request.Id).GreaterThan(0);
    }
}
