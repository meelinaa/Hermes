namespace Hermes.Application.Ports.Inbound;

using Hermes.Application.DTOs.Security;
using Hermes.Application.Services.Security;
using Hermes.Domain.ValueObjects;
/// <summary>Plain refresh returned once per issue/rotate; persistence is hash-only.</summary>
public interface IAuthTokenService
{
    ValueTask<AuthTokensResultDto> IssueTokensAsync(UserId userId, string? email, string? name, CancellationToken cancellationToken = default);

    /// <summary>null = invalid/replay/expired/concurrency race.</summary>
    ValueTask<AuthTokensResultDto?> RotateAsync(string refreshTokenPlain, CancellationToken cancellationToken = default);

    ValueTask<bool> TryRevokeRefreshForUserAsync(string refreshTokenPlain, UserId userId, CancellationToken cancellationToken = default);

    ValueTask RevokeAllForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
