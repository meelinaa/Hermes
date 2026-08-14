using Hermes.Domain.Events;
using MediatR;

namespace Hermes.Application.Ports.Inbound;

/// <summary>
/// Handler for a specific domain event.
/// </summary>
/// <typeparam name="TEvent">The type of the domain event.</typeparam>
public interface IDomainEventHandler<in TEvent> : INotificationHandler<TEvent> where TEvent : IDomainEvent
{
    // HandleAsync maps to MediatR's Handle
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);

    Task INotificationHandler<TEvent>.Handle(TEvent notification, CancellationToken cancellationToken) =>
        HandleAsync(notification, cancellationToken);
}
