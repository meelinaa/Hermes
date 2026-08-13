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
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("PasswordHash is required.", nameof(passwordHash));
        
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

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
    }

    /// <summary>Primary e-mail change clears <see cref="IsEmailVerified"/> until the new address is verified.</summary>
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

    public void VerifyEmail()
    {
        IsEmailVerified = true;
    }

    public void ReplacePasswordHash(string bcryptHash)
    {
        if (string.IsNullOrWhiteSpace(bcryptHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(bcryptHash));
        PasswordHash = bcryptHash;
    }

    public void EnableTwoFactor(string code, DateTime expiry)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code darf nicht leer sein.", nameof(code));
        
        TwoFactorCode = code;
        TwoFactorExpiry = expiry;
    }

    public void DisableTwoFactor()
    {
        TwoFactorCode = null;
        TwoFactorExpiry = null;
    }
}
