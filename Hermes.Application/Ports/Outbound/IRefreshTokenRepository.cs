using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

public interface IRefreshTokenRepository
{
    ValueTask AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
    ValueTask<RefreshToken?> GetActiveRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    ValueTask<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    ValueTask<bool> CompleteRefreshRotationAsync(RefreshToken trackedOld, RefreshToken newToken, CancellationToken cancellationToken = default);
    ValueTask RevokeRefreshTokenAsync(RefreshToken trackedToken, CancellationToken cancellationToken = default);
    ValueTask RevokeAllRefreshTokensForUserAsync(UserId userId, CancellationToken cancellationToken = default);
    ValueTask<List<RefreshToken>> GetAllRefreshTokensForUserAsync(UserId userId, CancellationToken cancellationToken = default);
    ValueTask UpdateTokensAsync(CancellationToken cancellationToken = default);
}
