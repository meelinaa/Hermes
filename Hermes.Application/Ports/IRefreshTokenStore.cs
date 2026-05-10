using Hermes.Domain.Entities;

namespace Hermes.Application.Ports;

/// <summary>Refresh-token rotation and revocation persistence.</summary>
public interface IRefreshTokenStore
{
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetActiveRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    /// <summary>
    /// Inserts <paramref name="newToken"/> and revokes <paramref name="trackedOld"/> in one transactional step when the old row is still active.
    /// Under concurrency, returns <c>false</c> if another caller already revoked the session (lost race); the INSERT is rolled back.
    /// </summary>
    Task<bool> CompleteRefreshRotationAsync(RefreshToken trackedOld, RefreshToken newToken, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(RefreshToken trackedToken, CancellationToken cancellationToken = default);
    Task RevokeAllRefreshTokensForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task RevokeTokenFamilyAsync(RefreshToken compromisedToken, CancellationToken cancellationToken = default);
}
