using System.Security.Cryptography;
using FluentResults;
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
    public async ValueTask<Result<AuthTokensResultDto>> IssueTokensAsync(UserId userId, string? email, string? name, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            return Result.Fail("User ID must be positive.");

        JwtAccessTokenResultDto access = jwt.Issue(userId, email, name);
        string? plain = CreateRefreshPlain();
        RefreshToken row = RefreshToken.Create(
            userId,
            RefreshTokenHashUtility.Hash(plain),
            timeProvider.GetUtcNow().UtcDateTime.AddDays(_o.RefreshTokenDays),
            timeProvider.GetUtcNow().UtcDateTime);
        await db.AddRefreshTokenAsync(row, cancellationToken).ConfigureAwait(false);
        return Result.Ok(new AuthTokensResultDto(
            access.Token,
            access.ExpiresAtUtc,
            plain,
            new DateTimeOffset(row.ExpiresAt, TimeSpan.Zero)));
    }

    /// <summary>
    /// Rotates an existing refresh token, issuing a new pair and revoking the old one.
    /// Returns failure when the token is invalid, expired or a replay attack is detected.
    /// </summary>
    public async ValueTask<Result<AuthTokensResultDto>> RotateAsync(string refreshTokenPlain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
            return Result.Fail("Refresh token cannot be empty.");

        string? hash = RefreshTokenHashUtility.Hash(refreshTokenPlain.Trim());
        RefreshToken? old = await db.GetRefreshTokenByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (old is null || old.User is null)
            return Result.Fail("Invalid token or user.");

        if (old.RevokedAt != null || old.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)
        {
            string shortHash = hash.Length > 8 ? hash[..8] + "..." : hash;
            logger.LogReplayDetected(old.UserId.Value, shortHash);
            await RevokeTokenFamilyAsync(old, cancellationToken).ConfigureAwait(false);
            return Result.Fail("Invalid token state. Token family revoked.");
        }

        string? newPlain = CreateRefreshPlain();
        RefreshToken newRow = RefreshToken.Create(
            old.UserId,
            RefreshTokenHashUtility.Hash(newPlain),
            timeProvider.GetUtcNow().UtcDateTime.AddDays(_o.RefreshTokenDays),
            timeProvider.GetUtcNow().UtcDateTime);
        bool rotated = await db.CompleteRefreshRotationAsync(old, newRow, cancellationToken).ConfigureAwait(false);
        if (!rotated)
            return Result.Fail("Token rotation conflict.");

        JwtAccessTokenResultDto access = jwt.Issue(old.User.Id, old.User.Email.Value, old.User.Name);
        return Result.Ok(new AuthTokensResultDto(
            access.Token,
            access.ExpiresAtUtc,
            newPlain,
            new DateTimeOffset(newRow.ExpiresAt, TimeSpan.Zero)));
    }

    /// <summary>
    /// Attempts to revoke a specific refresh token belonging to the given user.
    /// Returns false when the token is not found or does not belong to the user.
    /// </summary>
    public async ValueTask<Result> TryRevokeRefreshForUserAsync(string refreshTokenPlain, UserId userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenPlain))
            return Result.Fail("Refresh token cannot be empty.");

        string? hash = RefreshTokenHashUtility.Hash(refreshTokenPlain.Trim());
        RefreshToken? row = await db.GetActiveRefreshTokenByHashAsync(hash, cancellationToken).ConfigureAwait(false);
        if (row is null || row.UserId != userId)
            return Result.Fail("Token not found or does not belong to user.");

        await db.RevokeRefreshTokenAsync(row, cancellationToken).ConfigureAwait(false);
        return Result.Ok();
    }

    /// <summary>
    /// Revokes all refresh tokens for the given user.
    /// </summary>
    public async ValueTask<Result> RevokeAllForUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        await db.RevokeAllRefreshTokensForUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Result.Ok();
    }

    private async Task RevokeTokenFamilyAsync(RefreshToken compromisedToken, CancellationToken cancellationToken)
    {
        DateTime utc = timeProvider.GetUtcNow().UtcDateTime;

        List<RefreshToken> userTokens = await db.GetAllRefreshTokensForUserAsync(compromisedToken.UserId, cancellationToken).ConfigureAwait(false);

        var queue = new Queue<RefreshToken>();
        queue.Enqueue(compromisedToken);

        bool changesMade = false;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.RevokedAt == null)
            {
                current.Revoke(utc);
                changesMade = true;
            }

            if (current.ReplacedByTokenId is { } successorId)
            {
                RefreshToken? successor = userTokens.FirstOrDefault(t => t.Id == successorId);
                if (successor != null)
                    queue.Enqueue(successor);
            }
        }

        if (changesMade)
            await db.UpdateTokensAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>64 bytes cryptographically random, Base64 — opaque high-entropy refresh material.</summary>
    private static string CreateRefreshPlain() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
