using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Events;
using MediatR;

namespace Hermes.Infrastructure.EventDispatching;

/// <summary>
/// A lightweight domain event dispatcher that resolves handlers via MediatR.
/// </summary>
public sealed class DomainEventDispatcher(IPublisher publisher) : IDomainEventDispatcher
{
    public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        return publisher.Publish(domainEvent, cancellationToken);
    }
}
