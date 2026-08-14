using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;
/// <summary>Persisted refresh session: only hash stored; plaintext returned once per issue/rotate.</summary>
public class RefreshToken
{
    public int Id { get; private set; }

    public UserId UserId { get; private set; }

    /// <summary>SHA-256 hex of client refresh token (never persist plaintext).</summary>
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? RevokedAt { get; private set; }

    public int? ReplacedByTokenId { get; private set; }

    public RefreshToken? ReplacedByToken { get; private set; }

    public User? User { get; private set; }

    // EF Core constructor
    private RefreshToken() { }

    public static RefreshToken Create(UserId userId, string tokenHash, DateTime expiresAt, DateTime createdAt)
    {
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt
        };
    }

    public bool IsExpired(TimeProvider timeProvider) => timeProvider.GetUtcNow().UtcDateTime >= ExpiresAt;
    
    public bool IsRevoked => RevokedAt != null;
    
    public bool IsActive(TimeProvider timeProvider) => !IsRevoked && !IsExpired(timeProvider);

    public void Revoke(DateTime revokedAt, int? replacedByTokenId = null)
    {
        RevokedAt = revokedAt;
        ReplacedByTokenId = replacedByTokenId;
    }
}

