using FluentValidation;
using Hermes.Application.DTOs.Login;

namespace Hermes.Api.Validators.Auth;

/// <summary>
/// Validator for <see cref="LogoutRequestDto"/> to validate optional refresh token revocation payload.
/// </summary>
public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequestDto>
{
    /// <summary>
    /// Initializes validation rules ensuring refresh tokens do not exceed reasonable payload length bounds.
    /// </summary>
    public LogoutRequestValidator()
    {
        When(request => !string.IsNullOrEmpty(request.RefreshToken), () =>
        {
            RuleFor(request => request.RefreshToken)
                .MaximumLength(1024).WithMessage("Refresh token exceeds maximum allowed length.");
        });
    }
}
