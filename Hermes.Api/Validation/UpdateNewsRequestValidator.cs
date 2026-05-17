using FluentValidation;
using Hermes.Application.Models.News;

namespace Hermes.Api.Validation;

public sealed class UpdateNewsRequestValidator : AbstractValidator<UpdateNewsRequest>
{
    public UpdateNewsRequestValidator()
    {
        RuleFor(request => request.Id).GreaterThan(0);
    }
}
