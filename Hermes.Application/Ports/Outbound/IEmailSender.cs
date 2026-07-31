using Hermes.Application.DTOs.Email;

namespace Hermes.Application.Ports.Outbound;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
