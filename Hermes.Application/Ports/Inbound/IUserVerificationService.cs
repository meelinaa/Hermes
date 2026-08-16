using FluentResults;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Inbound;

/// <summary>
/// Inbound application port for initiating and validating user two-factor email ownership verification workflows.
/// </summary>
public interface IUserVerificationService
{
    /// <summary>
    /// Enqueues a background job to send a numeric verification challenge code to the specified email address.
    /// </summary>
    /// <param name="email">The user email address to verify.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A <see cref="Result"/> indicating success or a domain error (e.g. <see cref="Hermes.Application.Errors.UserNotFoundError"/>).</returns>
    Task<Result> SendVerificationMailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Validates a user-supplied 6-digit verification code against the stored challenge and completes verification if valid.
    /// </summary>
    /// <param name="userId">The unique identifier of the user completing verification.</param>
    /// <param name="code">The 6-digit integer verification code.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A <see cref="Result"/> indicating success or a domain error (e.g. <see cref="Hermes.Application.Errors.VerificationCodeMismatchError"/>).</returns>
    Task<Result> CheckVerificationCodeAsync(UserId userId, int code, CancellationToken cancellationToken = default);
}

