using System.Security.Cryptography;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Security;

/// <summary>
/// Implements refresh-token persistence and rotation on top of <see cref="IJwtTokenIssuer"/> for access tokens.
/// </summary>
public sealed class AuthTokenService(
    IHermesDataStore db,
    IJwtTokenIssuer jwt,
    IOptions<JwtOptions> options,
    ILogger<AuthTokenService> logger) : IAuthTokenService
{
    private readonly JwtOptions _o = options.Value;

    /// <summary>Issues a new access token and stores a hashed refresh token for the authenticated user.</summary>
    public async Task<AuthTokensResult> IssueTokensAsync(int userId, string? email, string? name, CancellationToken cancellationToken = default)
    {
        if(userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        JwtAccessTokenResult access = jwt.Issue(userId, email, name);
        string? plain = CreateRefreshPlain();
        RefreshToken row = new()
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

    /// <summary>Rotates a valid refresh token and returns the next access/refresh token pair.</summary>
    public async Task<AuthTokensResult?> RotateAsync(string refreshTokenPlain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
            return null;

        string? hash = RefreshTokenHasher.Hash(refreshTokenPlain.Trim());
        RefreshToken? old = await db.GetRefreshTokenByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (old is null || old.User is null)
            return null;

        if (old.RevokedAt != null || old.ExpiresAt <= DateTime.UtcNow)
        {
            string shortHash = hash.Length > 8 ? hash[..8] + "..." : hash;
            logger.LogWarning("Replay detected: Attempt to rotate revoked or expired token. UserId: {UserId}, TokenHash: {TokenHash}", old.UserId, shortHash);
            await db.RevokeTokenFamilyAsync(old, cancellationToken).ConfigureAwait(false);
            return null;
        }

        JwtAccessTokenResult access = jwt.Issue(old.User.Id, old.User.Email, old.User.Name);
        string? newPlain = CreateRefreshPlain();
        RefreshToken newRow = new()
        {
            UserId = old.UserId,
            TokenHash = RefreshTokenHasher.Hash(newPlain),
            ExpiresAt = DateTime.UtcNow.AddDays(_o.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow,
        };
        await db.CompleteRefreshRotationAsync(old, newRow, cancellationToken).ConfigureAwait(false);
        return new AuthTokensResult(
            access.Token,
            access.ExpiresAtUtc,
            newPlain,
            new DateTimeOffset(newRow.ExpiresAt, TimeSpan.Zero));
    }

    /// <summary>Revokes one refresh token if it belongs to the specified user.</summary>
    public async Task<bool> TryRevokeRefreshForUserAsync(string refreshTokenPlain, int userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
            return false;

        string? hash = RefreshTokenHasher.Hash(refreshTokenPlain.Trim());
        RefreshToken? row = await db.GetActiveRefreshTokenByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (row is null || row.UserId != userId)
            return false;

        await db.RevokeRefreshTokenAsync(row, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Revokes all active refresh tokens for the specified user.</summary>
    public Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        db.RevokeAllRefreshTokensForUserAsync(userId, cancellationToken);

    /// <summary>64 random bytes → Base64 string; unguessable refresh material.</summary>
    private static string CreateRefreshPlain() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
