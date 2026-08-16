using Hermes.Domain.ValueObjects;

namespace Hermes.Domain.Entities;

/// <summary>
/// Persisted refresh token entity representing an active or revoked refresh session.
/// Enforces SHA-256 token hashing, sliding window expiration, and absolute session lifetime bounds.
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// Gets the unique identifier of the refresh token record.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>
    /// Gets the associated user ID.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the SHA-256 hex hash of the plaintext refresh token.
    /// Plaintext tokens are never persisted.
    /// </summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the UTC expiration timestamp for the sliding token window.
    /// </summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>
    /// Gets the UTC creation timestamp of this specific token instance.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Gets the absolute UTC expiration timestamp for the overall session family.
    /// After this timestamp, no further rotations are permitted.
    /// </summary>
    public DateTime? AbsoluteExpiresAt { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when this token was revoked or rotated, or null if active.
    /// </summary>
    public DateTime? RevokedAt { get; private set; }

    /// <summary>
    /// Gets the ID of the successor token issued when this token was rotated.
    /// </summary>
    public int? ReplacedByTokenId { get; private set; }

    /// <summary>
    /// Gets the successor refresh token entity.
    /// </summary>
    public RefreshToken? ReplacedByToken { get; private set; }

    /// <summary>
    /// Gets the associated user navigation entity.
    /// </summary>
    public User? User { get; private set; }

    // EF Core constructor
    private RefreshToken() { }

    /// <summary>
    /// Factory method to create a new refresh token entity instance.
    /// </summary>
    /// <param name="userId">The ID of the owning user.</param>
    /// <param name="tokenHash">The SHA-256 hex hash of the plaintext token.</param>
    /// <param name="expiresAt">The sliding window expiration timestamp in UTC.</param>
    /// <param name="createdAt">The creation timestamp in UTC.</param>
    /// <param name="absoluteExpiresAt">Optional absolute session lifetime expiration timestamp in UTC.</param>
    /// <returns>A newly configured <see cref="RefreshToken"/> instance.</returns>
    public static RefreshToken Create(
        UserId userId,
        string tokenHash,
        DateTime expiresAt,
        DateTime createdAt,
        DateTime? absoluteExpiresAt = null)
    {
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            AbsoluteExpiresAt = absoluteExpiresAt
        };
    }

    /// <summary>
    /// Determines whether the token is expired based on either sliding window or absolute session expiration.
    /// </summary>
    /// <param name="timeProvider">The system time provider.</param>
    /// <returns>True if the token or session is expired; otherwise, false.</returns>
    public bool IsExpired(TimeProvider timeProvider)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        return now >= ExpiresAt || (AbsoluteExpiresAt != null && now >= AbsoluteExpiresAt.Value);
    }
    
    /// <summary>
    /// Gets a value indicating whether this token has been revoked or rotated.
    /// </summary>
    public bool IsRevoked => RevokedAt != null;
    
    /// <summary>
    /// Determines whether the token is currently active and eligible for rotation.
    /// </summary>
    /// <param name="timeProvider">The system time provider.</param>
    /// <returns>True if the token is not revoked and not expired; otherwise, false.</returns>
    public bool IsActive(TimeProvider timeProvider) => !IsRevoked && !IsExpired(timeProvider);

    /// <summary>
    /// Marks the token as revoked at the specified UTC timestamp and links its successor token ID if rotated.
    /// </summary>
    /// <param name="revokedAt">The UTC revocation timestamp.</param>
    /// <param name="replacedByTokenId">The ID of the successor token replacing this token.</param>
    public void Revoke(DateTime revokedAt, int? replacedByTokenId = null)
    {
        RevokedAt = revokedAt;
        ReplacedByTokenId = replacedByTokenId;
    }
}
