using Hermes.Application.DTOs.Email;

namespace Hermes.Application.Ports.Outbound;

public interface IEmailProvider
{
    Task SendAsync(EmailMessageDto message, CancellationToken cancellationToken = default);
}
