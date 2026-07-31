using Hermes.Application.DTOs.Email;

namespace Hermes.Application.Ports.Outbound;

public interface IEmailSender
{
    Task SendAsync(EmailMessageDto message, CancellationToken cancellationToken = default);
}
