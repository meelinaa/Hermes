using Hermes.Application.DTOs.Email;

namespace Hermes.Application.Ports;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
