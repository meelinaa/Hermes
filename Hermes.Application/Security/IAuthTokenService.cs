namespace Hermes.Application.Security;

/// <summary>Plain refresh returned once per issue/rotate; persistence is hash-only.</summary>
public interface IAuthTokenService
{
    Task<AuthTokensResult> IssueTokensAsync(int userId, string? email, string? name, CancellationToken cancellationToken = default);

    /// <summary>null = invalid/replay/expired/concurrency race.</summary>
    Task<AuthTokensResult?> RotateAsync(string refreshTokenPlain, CancellationToken cancellationToken = default);

    Task<bool> TryRevokeRefreshForUserAsync(string refreshTokenPlain, int userId, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken = default);
}
