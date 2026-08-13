namespace Hermes.Application.Ports.Inbound;

using Hermes.Domain.ValueObjects;

public interface IVerificationDigestService
{
    Task SendAsync(UserId userId, CancellationToken cancellationToken = default);
}
