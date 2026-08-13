using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Hermes.Application.EventHandlers;

public sealed class UserRegisteredEventHandler(
    IVerificationMailJobService verificationMailJobService,
    ILogger<UserRegisteredEventHandler> logger)
    : IDomainEventHandler<UserRegisteredEvent>
{
    public Task HandleAsync(UserRegisteredEvent domainEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling UserRegisteredEvent for user {UserId}. Enqueueing verification email.", domainEvent.UserId.Value);
        verificationMailJobService.EnqueueSendVerificationMail(domainEvent.UserId);
        return Task.CompletedTask;
    }
}
