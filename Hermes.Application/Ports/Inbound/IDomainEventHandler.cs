using Hermes.Domain.Events;

namespace Hermes.Application.Ports.Inbound;

/// <summary>
/// Inbound port for handling a specific domain event in the application layer.
/// </summary>
/// <typeparam name="TEvent">The type of the domain event.</typeparam>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles the specified domain event asynchronously.
    /// </summary>
    /// <param name="domainEvent">The domain event instance to process.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
