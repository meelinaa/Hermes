using FluentValidation;
using Hermes.Application.Models.NotificationLogs;

namespace Hermes.Api.Validation;

/// <summary>Rules for <see cref="CreateNotificationLogRequest"/>.</summary>
public sealed class CreateNotificationLogRequestValidator : AbstractValidator<CreateNotificationLogRequest>
{
    public CreateNotificationLogRequestValidator()
    {
        RuleFor(request => request.RetryCount).GreaterThanOrEqualTo(0);
    }
}
