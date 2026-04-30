namespace Hermes.Application.Services;

/// <summary>
/// Generates an e-mail verification code, persists expiry, renders <c>Verification.html</c>, and sends the message.
/// </summary>
public interface IVerificationDigestService
{
    /// <summary>
    /// Creates a new verification code (15-minute validity), stores it on the user, and sends the verification e-mail.
    /// </summary>
    Task SendAsync(int userId, CancellationToken cancellationToken = default);
}
