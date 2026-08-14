using FluentResults;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Inbound;

public interface IVerificationDigestService
{
    Task<Result<bool>> SendAsync(UserId userId, CancellationToken cancellationToken = default);
}
