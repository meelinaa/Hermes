using FluentValidation;
using Hermes.Application.DTOs.User;

namespace Hermes.Api.Validators.Auth;

/// <summary>
/// Validator for the RegisterUserRequestDto DTO to ensure user registration inputs are valid.
/// </summary>
public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequestDto>
{
    /// <summary>
    /// Initializes validation rules for name and password requirements in user registration.
    /// </summary>
    public RegisterUserRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Name is required.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
