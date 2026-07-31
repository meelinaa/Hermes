using FluentValidation;
using Hermes.Application.DTOs.User;

namespace Hermes.Api.Validation;

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
            .NotEmpty().WithMessage("Name is required.");

        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required.");

        RuleFor(request => request.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required when setting a new password.")
            .When(request => !string.IsNullOrWhiteSpace(request.NewPassword));
    }
}
