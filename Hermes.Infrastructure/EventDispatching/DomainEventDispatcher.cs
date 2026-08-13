using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.Infrastructure.EventDispatching;

/// <summary>
/// A lightweight domain event dispatcher that resolves handlers via IServiceProvider.
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
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
                    var task = (Task?)method.Invoke(handler, new object[] { domainEvent, cancellationToken });
                    if (task is not null)
                    {
                        await task.ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
