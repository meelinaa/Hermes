using Hermes.Application.Models.Email;

namespace Hermes.Application.Ports;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
