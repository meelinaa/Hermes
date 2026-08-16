using FluentValidation;
using Hermes.Application.DTOs.NotificationLogs;

namespace Hermes.Api.Validators.NotificationLogs;

/// <summary>
/// Validator for the <see cref="CreateNotificationLogRequestDto"/> to ensure retry count boundaries are valid.
/// </summary>
public sealed class CreateNotificationLogRequestValidator : AbstractValidator<CreateNotificationLogRequestDto>
{
    /// <summary>
    /// Initializes validation rules ensuring non-negative retry count values.
    /// </summary>
    public CreateNotificationLogRequestValidator()
    {
        RuleFor(request => request.RetryCount).GreaterThanOrEqualTo(0);

        RuleFor(request => request.Status)
            .IsInEnum().WithMessage("Invalid notification status specified.");

        RuleFor(request => request.Channel)
            .IsInEnum().WithMessage("Invalid delivery channel specified.");

        When(request => !string.IsNullOrEmpty(request.ErrorMessage), () =>
        {
            RuleFor(request => request.ErrorMessage)
                .MaximumLength(2000).WithMessage("Error message cannot exceed 2000 characters.");
        });
    }
}
