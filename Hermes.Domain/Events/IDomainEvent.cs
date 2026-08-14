using MediatR;

namespace Hermes.Domain.Events;

/// <summary>
/// Marker interface for all domain events.
/// </summary>
public interface IDomainEvent : INotification
{
}
