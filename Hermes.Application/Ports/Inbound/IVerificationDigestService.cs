namespace Hermes.Application.Services;

public interface IVerificationDigestService
{
    Task SendAsync(int userId, CancellationToken cancellationToken = default);
}
