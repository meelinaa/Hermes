using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Events;

/// <summary>
/// Domain event emitted when a user changes their primary email address.
/// </summary>
/// <param name="UserId">The unique identifier of the user.</param>
/// <param name="OldEmail">The previous email address.</param>
/// <param name="NewEmail">The newly set email address.</param>
public sealed record UserEmailChangedEvent(UserId UserId, string? OldEmail, string NewEmail) : IDomainEvent;
