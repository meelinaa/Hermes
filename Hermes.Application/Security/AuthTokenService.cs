using System.Security.Cryptography;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Security;

/// <summary>
/// Implements refresh-token persistence and rotation on top of <see cref="IJwtTokenIssuer"/> for access tokens.
/// </summary>
public sealed class AuthTokenService(
    IHermesDataStore db,
    IJwtTokenIssuer jwt,
    IOptions<JwtOptions> options) : IAuthTokenService
{
    private readonly JwtOptions _o = options.Value;

    /// <inheritdoc />
    public async Task<AuthTokensResult> IssueTokensAsync(int userId, string? email, string? name, CancellationToken cancellationToken = default)
    {
        if(userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        // Access: stateless JWT for API authorization.
        var access = jwt.Issue(userId, email, name);
        // Refresh: high-entropy random string shown once; only its hash is stored.
        var plain = CreateRefreshPlain();
        var row = new RefreshToken
        {
            UserId = userId,
            TokenHash = RefreshTokenHasher.Hash(plain),
            ExpiresAt = DateTime.UtcNow.AddDays(_o.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow,
        };
        await db.AddRefreshTokenAsync(row, cancellationToken).ConfigureAwait(false);
        return new AuthTokensResult(
            access.Token,
            access.ExpiresAtUtc,
            plain,
            new DateTimeOffset(row.ExpiresAt, TimeSpan.Zero));
    }

    /// <inheritdoc />
    public async Task<AuthTokensResult?> RotateAsync(string refreshTokenPlain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
            return null;

        var hash = RefreshTokenHasher.Hash(refreshTokenPlain.Trim());
        var old = await db.GetActiveRefreshTokenByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (old is null || old.User is null)
            return null;

        // New access token for the same user (claims refreshed from current user row).
        var access = jwt.Issue(old.User.Id, old.User.Email, old.User.Name);
        var newPlain = CreateRefreshPlain();
        var newRow = new RefreshToken
        {
            UserId = old.UserId,
            TokenHash = RefreshTokenHasher.Hash(newPlain),
            ExpiresAt = DateTime.UtcNow.AddDays(_o.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow,
        };
        // Old refresh must be revoked so the same token cannot be used twice (detect theft/replay).
        await db.CompleteRefreshRotationAsync(old, newRow, cancellationToken).ConfigureAwait(false);
        return new AuthTokensResult(
            access.Token,
            access.ExpiresAtUtc,
            newPlain,
            new DateTimeOffset(newRow.ExpiresAt, TimeSpan.Zero));
    }

    /// <inheritdoc />
    public async Task<bool> TryRevokeRefreshForUserAsync(string refreshTokenPlain, int userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
            return false;

        var hash = RefreshTokenHasher.Hash(refreshTokenPlain.Trim());
        var row = await db.GetActiveRefreshTokenByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        // Ensure the refresh belongs to the authenticated user (JWT sub) so users cannot revoke others' sessions.
        if (row is null || row.UserId != userId)
            return false;

        await db.RevokeRefreshTokenAsync(row, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        db.RevokeAllRefreshTokensForUserAsync(userId, cancellationToken);

    /// <summary>64 random bytes → Base64 string; unguessable refresh material.</summary>
    private static string CreateRefreshPlain() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
