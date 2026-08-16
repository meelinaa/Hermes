using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Outbound port for persisting and validating email verification challenge codes and completion state.
/// </summary>
public interface IUserVerificationStore
{
    /// <summary>
    /// Stores an email verification challenge code and its expiration timestamp on the user entity.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to challenge.</param>
    /// <param name="verificationCode">The generated verification code.</param>
    /// <param name="expiresAtUtc">The UTC expiration timestamp for the challenge.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask SetUserEmailVerificationChallengeAsync(UserId userId, string verificationCode, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the user's email address as verified after successfully validating the challenge code.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask CompleteUserEmailVerificationAsync(UserId userId, CancellationToken cancellationToken = default);
}
