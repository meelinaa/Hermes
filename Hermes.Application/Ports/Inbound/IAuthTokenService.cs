namespace Hermes.Application.Ports.Inbound;

using Hermes.Application.DTOs.Security;
using Hermes.Application.Services.Security;
/// <summary>Plain refresh returned once per issue/rotate; persistence is hash-only.</summary>
public interface IAuthTokenService
{
    ValueTask<AuthTokensResultDto> IssueTokensAsync(int userId, string? email, string? name, CancellationToken cancellationToken = default);

    /// <summary>null = invalid/replay/expired/concurrency race.</summary>
    ValueTask<AuthTokensResultDto?> RotateAsync(string refreshTokenPlain, CancellationToken cancellationToken = default);

    ValueTask<bool> TryRevokeRefreshForUserAsync(string refreshTokenPlain, int userId, CancellationToken cancellationToken = default);

    ValueTask RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default);
}
