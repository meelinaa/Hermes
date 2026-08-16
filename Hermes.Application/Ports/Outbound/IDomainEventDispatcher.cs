using Hermes.Domain.Events;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Dispatches domain events to their respective handlers.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches an event to all registered handlers for its type.
    /// </summary>
    /// <param name="domainEvent">The domain event to dispatch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
