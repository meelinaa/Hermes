using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;
/// <summary>Persisted refresh session: only hash stored; plaintext returned once per issue/rotate.</summary>
public class RefreshToken
{
    public int Id { get; set; }

    public UserId UserId { get; set; }

    /// <summary>SHA-256 hex of client refresh token (never persist plaintext).</summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public int? ReplacedByTokenId { get; set; }

    public RefreshToken? ReplacedByToken { get; set; }

    public User? User { get; set; }
}
