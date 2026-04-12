using FluentValidation;
using Hermes.Application.Models;

namespace Hermes.Api.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.NameOrEmail).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
