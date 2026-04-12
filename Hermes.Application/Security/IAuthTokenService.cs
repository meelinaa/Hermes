namespace Hermes.Application.Security;

/// <summary>
/// Orchestrates JWT access tokens and opaque refresh tokens: issue on login, rotate on refresh, revoke on logout.
/// Refresh tokens are persisted only as hashes; the plain value is returned once per issuance or rotation.
/// </summary>
public interface IAuthTokenService
{
    /// <summary>
    /// After successful authentication: creates a new JWT via <see cref="IJwtTokenIssuer"/> and a new refresh token row in the database.
    /// </summary>
    Task<AuthTokensResult> IssueTokensAsync(int userId, string? email, string? name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts the plain refresh token from the client, looks up the hash, and if valid: revokes the old row, inserts a new refresh row,
    /// and returns a fresh JWT + new refresh (rotation). Returns <c>null</c> if missing, expired, or already revoked (reuse attack).
    /// </summary>
    Task<AuthTokensResult?> RotateAsync(string refreshTokenPlain, CancellationToken cancellationToken = default);

    /// <summary>Revokes one refresh token after verifying it belongs to <paramref name="userId"/> (logout single session).</summary>
    Task<bool> TryRevokeRefreshForUserAsync(string refreshTokenPlain, int userId, CancellationToken cancellationToken = default);

    /// <summary>Revokes all non-expired refresh tokens for the user (logout everywhere).</summary>
    Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default);
}
