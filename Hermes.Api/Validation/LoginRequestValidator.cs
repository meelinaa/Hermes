using FluentValidation;
using Hermes.Application.Models.Login;

namespace Hermes.Api.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(loginRequest => loginRequest.NameOrEmail).NotEmpty();
        RuleFor(loginRequest => loginRequest.Password).NotEmpty();
    }
}
