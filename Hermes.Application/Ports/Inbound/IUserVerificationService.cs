namespace Hermes.Application.Ports.Inbound;

using Hermes.Domain.ValueObjects;

public interface IUserVerificationService
{
    Task SendVerificationMailAsync(string email, CancellationToken cancellationToken);

    Task CheckVerificationCodeAsync(UserId userId, int code, CancellationToken cancellationToken = default);
}
