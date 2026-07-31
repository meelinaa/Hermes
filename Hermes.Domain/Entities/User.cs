using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsEmailVerified { get; set; }
    public string? TwoFactorCode { get; set; }
    public DateTime? TwoFactorExpiry { get; set; }

    public ICollection<NewsletterSubscription> NewsletterSubscriptions { get; set; } = [];

    public ICollection<NotificationLog> NotificationLogs { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
    }

    /// <summary>Primary e-mail change clears <see cref="IsEmailVerified"/> until the new address is verified.</summary>
    public void ChangePrimaryEmail(Email email)    {
        string next = email.Value;
        string? previous = Email;
        Email = next;
        if (!string.Equals(previous, next, StringComparison.Ordinal))
            IsEmailVerified = false;
    }

    public void ReplacePasswordHash(string bcryptHash)
    {
        ArgumentNullException.ThrowIfNull(bcryptHash);
        PasswordHash = bcryptHash;
    }
}
