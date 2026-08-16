using FluentResults;
using Hermes.Application.DTOs.Security;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Inbound;

public interface IAuthTokenService
{
    ValueTask<Result<AuthTokensResultDto>> IssueTokensAsync(UserId userId, string? email, string? name, CancellationToken cancellationToken = default);
    ValueTask<Result<AuthTokensResultDto>> RotateAsync(string refreshTokenPlain, CancellationToken cancellationToken = default);
    ValueTask<Result> TryRevokeRefreshForUserAsync(string refreshTokenPlain, UserId userId, CancellationToken cancellationToken = default);
    ValueTask<Result> RevokeAllForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
