using Hermes.Domain.Entities;

namespace Hermes.Application.Ports.Outbound;

public interface IRefreshTokenStore
{
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetActiveRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    /// <summary>Atomic rotate: false if concurrent revoke won the race (insert rolled back).</summary>
    Task<bool> CompleteRefreshRotationAsync(RefreshToken trackedOld, RefreshToken newToken, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(RefreshToken trackedToken, CancellationToken cancellationToken = default);
    Task RevokeAllRefreshTokensForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task RevokeTokenFamilyAsync(RefreshToken compromisedToken, CancellationToken cancellationToken = default);
}
