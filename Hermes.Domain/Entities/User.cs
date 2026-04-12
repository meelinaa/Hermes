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
}
