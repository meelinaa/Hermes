using Hermes.Notifications.Sending.Models;

namespace Hermes.Notifications.Sending;

/// <summary>
/// Sends e-mail messages using a configured transport.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends the specified message.
    /// </summary>
    /// <param name="message">Message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the message has been sent.</returns>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
