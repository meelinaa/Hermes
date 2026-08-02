using FluentValidation;
using Hermes.Application.DTOs.Login;

namespace Hermes.Api.Validators.Auth;

/// <summary>
/// Validator for the <see cref="LoginRequestDto"/> to ensure login request inputs are valid.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    /// <summary>
    /// Initializes validation rules for username/email and password requirement checks.
    /// </summary>
    public LoginRequestValidator()
    {
        RuleFor(loginRequest => loginRequest.NameOrEmail).NotEmpty();
        RuleFor(loginRequest => loginRequest.Password).NotEmpty();
    }
}
