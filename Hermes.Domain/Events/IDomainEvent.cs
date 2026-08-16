namespace Hermes.Domain.Events;

/// <summary>
/// Pure POCO marker interface for all domain events emitted by domain entities and aggregates.
/// Completely decoupled from third-party messaging libraries and frameworks.
/// </summary>
public interface IDomainEvent
{
}
