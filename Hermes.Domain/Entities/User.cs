using Hermes.Domain.Exceptions;
using System;
using System.Collections.Generic;
using Hermes.Domain.Events;
using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;

public class User : AggregateRoot
{
    private readonly List<NewsletterSubscription> _newsletterSubscriptions = [];
    private readonly List<NotificationLog> _notificationLogs = [];
    private readonly List<RefreshToken> _refreshTokens = [];

    // Internal parameterless constructor for EF Core and Unit Tests
    internal User() { }

    private User(string name, Email email, string passwordHash)
    {
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        IsEmailVerified = false;
        
        AddDomainEvent(new UserRegisteredEvent(Id, email.Value));
    }

    public static User Create(string name, Email email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException("Name is required.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainValidationException("PasswordHash is required.");
        
        return new User(name.Trim(), email, passwordHash);
    }

    public UserId Id { get; internal set; }
    public string Name { get; internal set; } = string.Empty;
    public Email Email { get; internal set; } = default!;
    public string PasswordHash { get; internal set; } = string.Empty;
    public bool IsEmailVerified { get; internal set; }
    public string? TwoFactorCode { get; internal set; }
    public DateTime? TwoFactorExpiry { get; internal set; }

    public IReadOnlyList<NewsletterSubscription> NewsletterSubscriptions => _newsletterSubscriptions.AsReadOnly();
    public IReadOnlyList<NotificationLog> NotificationLogs => _notificationLogs.AsReadOnly();
    public IReadOnlyList<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    /// <summary>
    /// Updates the user's display name, enforcing non-whitespace constraints.
    /// </summary>
    /// <param name="name">The new display name.</param>
    /// <exception cref="DomainValidationException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException("Name is required.");
        Name = name.Trim();
    }

    /// <summary>
    /// Updates the user's primary email address, resetting email verification status if the address changed,
    /// and emitting a <see cref="UserEmailChangedEvent"/> domain event.
    /// </summary>
    /// <param name="nextEmail">The new primary email value object.</param>
    public void ChangePrimaryEmail(Email nextEmail)
    {
        Email previous = Email;
        Email = nextEmail;
        if (!string.Equals(previous.Value, nextEmail.Value, StringComparison.OrdinalIgnoreCase))
        {
            IsEmailVerified = false;
            // The event uses the string values
            AddDomainEvent(new UserEmailChangedEvent(Id, previous.Value, nextEmail.Value));
        }
    }

    /// <summary>
    /// Marks the user's primary email address as verified upon successful OTP confirmation.
    /// </summary>
    public void VerifyEmail()
    {
        IsEmailVerified = true;
    }

    /// <summary>
    /// Replaces the BCrypt password hash when a user changes their credentials.
    /// </summary>
    /// <param name="bcryptHash">The newly computed BCrypt hash string.</param>
    /// <exception cref="DomainValidationException">Thrown when <paramref name="bcryptHash"/> is null or whitespace.</exception>
    public void ReplacePasswordHash(string bcryptHash)
    {
        if (string.IsNullOrWhiteSpace(bcryptHash))
            throw new DomainValidationException("Password hash cannot be empty.");
        PasswordHash = bcryptHash;
    }

    /// <summary>
    /// Stores an active two-factor challenge code and its expiration timestamp on the user aggregate.
    /// </summary>
    /// <param name="code">The verification challenge string (hash or OTP).</param>
    /// <param name="expiry">The UTC timestamp when the challenge expires.</param>
    /// <exception cref="DomainValidationException">Thrown when <paramref name="code"/> is null or whitespace.</exception>
    public void EnableTwoFactor(string code, DateTime expiry)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainValidationException("Code cannot be empty.");
        
        TwoFactorCode = code;
        TwoFactorExpiry = expiry;
    }

    /// <summary>
    /// Clears any active two-factor challenge code and expiration timestamp.
    /// </summary>
    public void DisableTwoFactor()
    {
        TwoFactorCode = null;
        TwoFactorExpiry = null;
    }
}
