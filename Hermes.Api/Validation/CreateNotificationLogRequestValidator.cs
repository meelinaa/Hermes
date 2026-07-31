using FluentValidation;
using Hermes.Application.DTOs.NotificationLogs;

namespace Hermes.Api.Validation;

public sealed class CreateNotificationLogRequestValidator : AbstractValidator<CreateNotificationLogRequest>
{
    public CreateNotificationLogRequestValidator()
    {
        RuleFor(request => request.RetryCount).GreaterThanOrEqualTo(0);
    }
}
