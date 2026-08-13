using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Events;

/// <summary>
/// Domain event emitted when a new user account is successfully registered.
/// </summary>
/// <param name="UserId">The unique identifier of the new user.</param>
/// <param name="Email">The primary email address of the new user.</param>
public sealed record UserRegisteredEvent(UserId UserId, string Email) : IDomainEvent;
