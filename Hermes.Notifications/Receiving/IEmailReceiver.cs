using Hermes.Notifications.Receiving.Models;

namespace Hermes.Notifications.Receiving;

/// <summary>
/// Retrieves e-mail messages from a server or test harness (e.g. MailHog).
/// </summary>
public interface IEmailReceiver
{
    /// <summary>
    /// Returns the most recently received message.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest message.</returns>
    /// <exception cref="InvalidOperationException">Thrown when there are no messages.</exception>
    Task<EmailResult> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all stored messages (may perform multiple API requests if paginated).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All messages returned by the server.</returns>
    Task<IEnumerable<EmailResult>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns messages whose subject contains <paramref name="subject"/> (case-insensitive).
    /// </summary>
    /// <param name="subject">Substring to match in the subject.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Messages matching the subject filter.</returns>
    Task<IEnumerable<EmailResult>> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default);
}
