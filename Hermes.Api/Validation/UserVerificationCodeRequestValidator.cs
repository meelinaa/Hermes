using FluentValidation;
using Hermes.Application.Models.User;

namespace Hermes.Api.Validation;

/// <summary>
/// Validator for the UserVerificationCodeRequest DTO to ensure email verification codes are valid.
/// </summary>
public sealed class UserVerificationCodeRequestValidator : AbstractValidator<UserVerificationCodeRequest>
{
    /// <summary>
    /// Initializes validation rules for verification code payload requirements.
    /// </summary>
    public UserVerificationCodeRequestValidator()
    {
        RuleFor(request => request.UserId)
            .GreaterThan(0).WithMessage("A valid user id is required.");

        RuleFor(request => request.Code)
            .InclusiveBetween(0, 999_999).WithMessage("Verification code must be between 0 and 999999.");
    }
}
