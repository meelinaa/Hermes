using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Infrastructure.EventDispatching;

/// <summary>
/// Lightweight domain event dispatcher that resolves and invokes registered <see cref="IDomainEventHandler{TEvent}"/> instances via DI.
/// Decouples domain event dispatching from external messaging libraries.
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches a domain event to all registered domain event handlers in the service provider.
    /// </summary>
    /// <param name="domainEvent">The domain event to dispatch.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A Task representing the asynchronous dispatch operation.</returns>
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        Type eventType = domainEvent.GetType();
        Type handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        IEnumerable<object?> handlers = serviceProvider.GetServices(handlerType);
        foreach (object? handler in handlers)
        {
            if (handler is not null)
            {
                var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));
                if (method is not null)
                {
                    var task = (Task?)method.Invoke(handler, [domainEvent, cancellationToken]);
                    if (task is not null)
                    {
                        await task.ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
