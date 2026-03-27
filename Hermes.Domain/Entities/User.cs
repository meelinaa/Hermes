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

    public News? NewsSettings { get; set; }
    public ICollection<NotificationLog> NotificationLogs { get; set; } = new List<NotificationLog>();
}
