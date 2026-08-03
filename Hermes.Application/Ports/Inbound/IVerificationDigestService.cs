namespace Hermes.Application.Ports.Inbound;

public interface IVerificationDigestService
{
    Task SendAsync(int userId, CancellationToken cancellationToken = default);
}
