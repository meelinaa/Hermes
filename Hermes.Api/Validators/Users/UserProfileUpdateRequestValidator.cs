using FluentValidation;
using Hermes.Application.DTOs.User;

namespace Hermes.Api.Validators.Users;

/// <summary>
/// Validator for the UserProfileUpdateRequestDto DTO to ensure update parameters are valid.
/// </summary>
public sealed class UserProfileUpdateRequestValidator : AbstractValidator<UserProfileUpdateRequestDto>
{
    /// <summary>
    /// Initializes validation rules for profile update requirements including conditional checks for password updates.
    /// </summary>
    public UserProfileUpdateRequestValidator()
    {
        RuleFor(request => request.Id)
            .GreaterThan(0).WithMessage("User Id is required for update.");

        RuleFor(request => request.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(255).WithMessage("Email cannot exceed 255 characters.");

        RuleFor(request => request.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required when setting a new password.")
            .When(request => !string.IsNullOrWhiteSpace(request.NewPassword));

        When(request => !string.IsNullOrWhiteSpace(request.NewPassword), () =>
        {
            RuleFor(request => request.NewPassword)
                .MinimumLength(8).WithMessage("New password must be at least 8 characters.")
                .MaximumLength(128).WithMessage("New password cannot exceed 128 characters.");
        });
    }
}
