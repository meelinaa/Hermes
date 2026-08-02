namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Defines the contract for background services handling the delivery of verification emails.
/// </summary>
public interface IVerificationMailJobService
{
    /// <summary>
    /// Enqueues a background job to send a verification email to the specified user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to send the verification email to.</param>
    /// <returns>The unique identifier of the enqueued job, or null if the job could not be enqueued.</returns>
    string? EnqueueSendVerificationMail(int userId);
}
