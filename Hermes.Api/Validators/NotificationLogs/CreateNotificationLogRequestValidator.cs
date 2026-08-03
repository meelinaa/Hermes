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
    }
}
