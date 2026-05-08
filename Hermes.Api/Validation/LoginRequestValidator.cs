using FluentValidation;
using Hermes.Application.Models;

namespace Hermes.Api.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Initializes validation rules for login requests.</summary>
    public LoginRequestValidator()
    {
        RuleFor(loginRequest => loginRequest.NameOrEmail).NotEmpty();
        RuleFor(loginRequest => loginRequest.Password).NotEmpty();
    }
}
