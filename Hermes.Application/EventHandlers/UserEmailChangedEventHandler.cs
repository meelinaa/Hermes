using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Hermes.Application.EventHandlers;

public sealed class UserEmailChangedEventHandler(
    IVerificationMailJobService verificationMailJobService,
    ILogger<UserEmailChangedEventHandler> logger)
    : IDomainEventHandler<UserEmailChangedEvent>
{
    public Task HandleAsync(UserEmailChangedEvent domainEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling UserEmailChangedEvent for user {UserId}. Enqueueing new verification email.", domainEvent.UserId.Value);
        verificationMailJobService.EnqueueSendVerificationMail(domainEvent.UserId);
        return Task.CompletedTask;
    }
}
