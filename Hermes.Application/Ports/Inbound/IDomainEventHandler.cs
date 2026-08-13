using Hermes.Domain.Events;

namespace Hermes.Application.Ports.Inbound;

/// <summary>
/// Handler for a specific domain event.
/// </summary>
/// <typeparam name="TEvent">The type of the domain event.</typeparam>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles the specified domain event.
    /// </summary>
    /// <param name="domainEvent">The event to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
