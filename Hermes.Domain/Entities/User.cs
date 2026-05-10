using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsEmailVerified { get; set; }   // 2FA für Email
    public string? TwoFactorCode { get; set; }  // temporärer 2FA Code
    public DateTime? TwoFactorExpiry { get; set; } // wann läuft der Code ab

    /// <summary>News subscription/configuration rows owned by this user (one-to-many).</summary>
    public ICollection<News> News { get; set; } = [];

    public ICollection<NotificationLog> NotificationLogs { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    /// <summary>Updates display name (trimmed).</summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
    }

    /// <summary>Sets primary e-mail (already normalized via <see cref="Email"/>). Clears verification when the address changes.</summary>
    public void ChangePrimaryEmail(Email email)
    {
        string next = email.Value;
        string? previous = Email;
        Email = next;
        if (!string.Equals(previous, next, StringComparison.Ordinal))
            IsEmailVerified = false;
    }

    /// <summary>Replaces stored password hash after application-layer hashing.</summary>
    public void ReplacePasswordHash(string bcryptHash)
    {
        ArgumentNullException.ThrowIfNull(bcryptHash);
        PasswordHash = bcryptHash;
    }
}
