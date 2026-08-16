using Hermes.WebFrontend.Client.ApiModels;

namespace Hermes.WebFrontend.Client.Services.Api;

/// <summary>
/// Strongly typed client interface for user profile, security, and verification endpoints.
/// </summary>
public interface IUserApiClient
{
    /// <summary>
    /// Retrieves user profile information by numeric user ID.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing user profile details on success.</returns>
    Task<ApiResult<UserScopeDto>> GetUserProfileAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates user profile details or password.
    /// </summary>
    /// <param name="request">The update payload containing name, email, and optional passwords.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing the updated user details on success.</returns>
    Task<ApiResult<UserScopeDto>> UpdateUserAsync(UserProfileUpdateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests dispatch of a verification OTP email.
    /// </summary>
    /// <param name="userId">The ID of the user requesting verification.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing email dispatch information on success.</returns>
    Task<ApiResult<SendVerificationMailResponseDto>> SendEmailVerificationCodeAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the OTP code submitted by the user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="code">The OTP verification code.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing updated user details on success.</returns>
    Task<ApiResult<UserScopeDto>> ConfirmEmailVerificationCodeAsync(int userId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes the user account.
    /// </summary>
    /// <param name="userId">The ID of the user to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result indicating success or failure.</returns>
    Task<ApiResult> DeleteAccountAsync(int userId, CancellationToken cancellationToken = default);
}
