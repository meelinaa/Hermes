using System.Security.Cryptography;
using Hermes.Application.DTOs.Security;
using Hermes.Application.Options.Auth;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Hermes.Application.Logging;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services.Security;

/// <summary>
/// Handles issuance, rotation and revocation of JWT access tokens and refresh tokens.
/// </summary>
public sealed class AuthTokenService(
    IRefreshTokenRepository db,
    IJwtTokenProvider jwt,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider,
    ILogger<AuthTokenService> logger) : IAuthTokenService
{
    private readonly JwtOptions _o = options.Value;

    /// <summary>
    /// Issues a new JWT access token and a persisted refresh token for the given user.
    /// </summary>
    public async ValueTask<AuthTokensResultDto> IssueTokensAsync(UserId userId, string? email, string? name, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        JwtAccessTokenResultDto access = jwt.Issue(userId, email, name);
        string? plain = CreateRefreshPlain();
        RefreshToken row = new()
        {
            UserId = userId,
            TokenHash = RefreshTokenHashUtility.Hash(plain),
            ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(_o.RefreshTokenDays),
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        await db.AddRefreshTokenAsync(row, cancellationToken).ConfigureAwait(false);
        return new AuthTokensResultDto(
            access.Token,
            access.ExpiresAtUtc,
            plain,
            new DateTimeOffset(row.ExpiresAt, TimeSpan.Zero));
    }

    /// <summary>
    /// Rotates an existing refresh token, issuing a new pair and revoking the old one.
    /// Returns null when the token is invalid, expired or a replay attack is detected.
    /// </summary>
    public async ValueTask<AuthTokensResultDto?> RotateAsync(string refreshTokenPlain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
            return null;

        string? hash = RefreshTokenHashUtility.Hash(refreshTokenPlain.Trim());
        RefreshToken? old = await db.GetRefreshTokenByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (old is null || old.User is null)
            return null;

        if (old.RevokedAt != null || old.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)
        {
            string shortHash = hash.Length > 8 ? hash[..8] + "..." : hash;
            logger.LogReplayDetected(old.UserId.Value, shortHash);
            await db.RevokeTokenFamilyAsync(old, cancellationToken).ConfigureAwait(false);
            return null;
        }

        string? newPlain = CreateRefreshPlain();
        RefreshToken newRow = new()
        {
            UserId = old.UserId,
            TokenHash = RefreshTokenHashUtility.Hash(newPlain),
            ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(_o.RefreshTokenDays),
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        bool rotated = await db.CompleteRefreshRotationAsync(old, newRow, cancellationToken).ConfigureAwait(false);
        if (!rotated)
            return null;

        JwtAccessTokenResultDto access = jwt.Issue(old.User.Id, old.User.Email.Value, old.User.Name);
        return new AuthTokensResultDto(
            access.Token,
            access.ExpiresAtUtc,
            newPlain,
            new DateTimeOffset(newRow.ExpiresAt, TimeSpan.Zero));
    }

    /// <summary>
    /// Attempts to revoke a specific refresh token belonging to the given user.
    /// Returns false when the token is not found or does not belong to the user.
    /// </summary>
    public async ValueTask<bool> TryRevokeRefreshForUserAsync(string refreshTokenPlain, UserId userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
            return false;

        string? hash = RefreshTokenHashUtility.Hash(refreshTokenPlain.Trim());
        RefreshToken? row = await db.GetActiveRefreshTokenByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (row is null || row.UserId != userId)
            return false;

        await db.RevokeRefreshTokenAsync(row, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Revokes all refresh tokens for the given user.
    /// </summary>
    public ValueTask RevokeAllForUserAsync(UserId userId, CancellationToken cancellationToken = default) =>
        db.RevokeAllRefreshTokensForUserAsync(userId, cancellationToken);

    /// <summary>64 bytes cryptographically random, Base64 — opaque high-entropy refresh material.</summary>
    private static string CreateRefreshPlain() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
