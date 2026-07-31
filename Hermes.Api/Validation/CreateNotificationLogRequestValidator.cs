using FluentValidation;
using Hermes.Application.DTOs.NotificationLogs;

namespace Hermes.Api.Validation;

public sealed class CreateNotificationLogRequestValidator : AbstractValidator<CreateNotificationLogRequestDto>
{
    public CreateNotificationLogRequestValidator()
    {
        RuleFor(request => request.RetryCount).GreaterThanOrEqualTo(0);
    }
}
