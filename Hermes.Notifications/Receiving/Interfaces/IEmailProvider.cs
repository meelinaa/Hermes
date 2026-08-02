using Hermes.Notifications.Receiving.DTOs;

namespace Hermes.Notifications.Receiving.Interfaces;

/// <summary>
/// Interface for reading and querying emails received by a test mail sink (e.g. MailHog).
/// </summary>
public interface IEmailProvider
{
    /// <summary>
    /// Retrieves the most recently received email from the mail sink.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest email result DTO.</returns>
    Task<EmailResultDto> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all received emails from the mail sink.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of email result DTOs.</returns>
    Task<IEnumerable<EmailResultDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves emails whose subject line contains the specified substring.
    /// </summary>
    /// <param name="subject">The subject substring to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of matching email result DTOs.</returns>
    Task<IEnumerable<EmailResultDto>> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default);
}
