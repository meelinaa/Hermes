namespace Hermes.Domain.Entities;

/// <summary>
/// Opaque refresh session: only a hash of the client token is stored. Plain text is returned once at creation or rotation.
/// </summary>
public class RefreshToken
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>Owner user; must match the authenticated principal when revoking a specific refresh.</summary>
    public int UserId { get; set; }

    /// <summary>SHA-256 hex hash of the refresh token string (never store the plain token).</summary>
    public string TokenHash { get; set; } = "";

    /// <summary>UTC instant after which this row cannot be used for rotation even if not revoked.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC instant when the row was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When set, the refresh is no longer valid (logout or rotation replaced it).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Optional link to the replacement row after refresh-token rotation (audit).</summary>
    public int? ReplacedByTokenId { get; set; }

    public User? User { get; set; }
}
