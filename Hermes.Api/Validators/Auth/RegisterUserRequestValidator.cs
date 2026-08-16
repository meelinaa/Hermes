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
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password cannot exceed 128 characters.");
    }
}
