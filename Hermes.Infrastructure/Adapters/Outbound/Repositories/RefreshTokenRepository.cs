using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Infrastructure.Adapters.Outbound.Repositories;

/// <inheritdoc />
public sealed class RefreshTokenRepository(HermesDbContext db, TimeProvider timeProvider) : IRefreshTokenRepository
{
    /// <inheritdoc />
    public async ValueTask<RefreshToken?> GetActiveRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tokenHash))
            return null;
        DateTime utc = timeProvider.GetUtcNow().UtcDateTime;
        return await db.RefreshTokens
            .Include(refreshToken => refreshToken.User)
            .FirstOrDefaultAsync(
                refreshToken => refreshToken.TokenHash == tokenHash && refreshToken.RevokedAt == null && refreshToken.ExpiresAt > utc,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tokenHash))
            return null;
        return await db.RefreshTokens
            .Include(refreshToken => refreshToken.User)
            .FirstOrDefaultAsync(
                refreshToken => refreshToken.TokenHash == tokenHash,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> CompleteRefreshRotationAsync(RefreshToken trackedOld, RefreshToken newToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trackedOld);
        ArgumentNullException.ThrowIfNull(newToken);

        DateTime utc = timeProvider.GetUtcNow().UtcDateTime;
        int oldId = trackedOld.Id;
        string expectedHash = trackedOld.TokenHash;

        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await db.RefreshTokens.AddAsync(newToken, cancellationToken).ConfigureAwait(false);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                int rows = await db.RefreshTokens
                    .Where(
                        t => t.Id == oldId
                            && t.TokenHash == expectedHash
                            && t.RevokedAt == null
                            && t.ExpiresAt > utc)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(t => t.RevokedAt, utc).SetProperty(t => t.ReplacedByTokenId, newToken.Id),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (rows != 1)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return false;
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        await db.RefreshTokens.AddAsync(newToken, cancellationToken).ConfigureAwait(false);
        trackedOld.Revoke(utc, newToken.Id);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async ValueTask RevokeRefreshTokenAsync(RefreshToken trackedToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trackedToken);
        trackedToken.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        await db.RefreshTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RevokeAllRefreshTokensForUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        DateTime utc = timeProvider.GetUtcNow().UtcDateTime;
        List<RefreshToken> active = await db.RefreshTokens
            .Where(refreshToken => refreshToken.UserId == userId && refreshToken.RevokedAt == null && refreshToken.ExpiresAt > utc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (RefreshToken activeToken in active)
            activeToken.Revoke(utc);
        if (active.Count > 0)
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<List<RefreshToken>> GetAllRefreshTokensForUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        return await db.RefreshTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask UpdateTokensAsync(CancellationToken cancellationToken = default)
    {
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
