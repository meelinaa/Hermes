using System.Security.Cryptography;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Security;

public sealed class AuthTokenService(
    IRefreshTokenRepository db,
    IJwtTokenIssuer jwt,
    IOptions<JwtOptions> options,
    ILogger<AuthTokenService> logger) : IAuthTokenService
{
    private readonly JwtOptions _o = options.Value;

    public async Task<AuthTokensResultDto> IssueTokensAsync(int userId, string? email, string? name, CancellationToken cancellationToken = default)
    {
        if(userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        JwtAccessTokenResultDto access = jwt.Issue(userId, email, name);
        string? plain = CreateRefreshPlain();
        RefreshToken row = new()
        {
            UserId = userId,
            TokenHash = RefreshTokenHasher.Hash(plain),
            ExpiresAt = DateTime.UtcNow.AddDays(_o.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow,
        };
        await db.AddRefreshTokenAsync(row, cancellationToken).ConfigureAwait(false);
        return new AuthTokensResultDto(
            access.Token,
            access.ExpiresAtUtc,
            plain,
            new DateTimeOffset(row.ExpiresAt, TimeSpan.Zero));
    }

    public async Task<AuthTokensResultDto?> RotateAsync(string refreshTokenPlain, CancellationToken cancellationToken = default)
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

        string? newPlain = CreateRefreshPlain();
        RefreshToken newRow = new()
        {
            UserId = old.UserId,
            TokenHash = RefreshTokenHasher.Hash(newPlain),
            ExpiresAt = DateTime.UtcNow.AddDays(_o.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow,
        };
        bool rotated = await db.CompleteRefreshRotationAsync(old, newRow, cancellationToken).ConfigureAwait(false);
        if (!rotated)
            return null;

        JwtAccessTokenResultDto access = jwt.Issue(old.User.Id, old.User.Email, old.User.Name);
        return new AuthTokensResultDto(
            access.Token,
            access.ExpiresAtUtc,
            newPlain,
            new DateTimeOffset(newRow.ExpiresAt, TimeSpan.Zero));
    }

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

    public Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        db.RevokeAllRefreshTokensForUserAsync(userId, cancellationToken);

    /// <summary>64 bytes cryptographically random, Base64 — opaque high-entropy refresh material.</summary>
    private static string CreateRefreshPlain() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
