namespace Hermes.Application.Ports.Inbound;

public interface IUserVerificationService
{
    Task SendVerificationMailAsync(string email, CancellationToken cancellationToken);

    Task CheckVerificationCodeAsync(int userId, int code, CancellationToken cancellationToken = default);
}
