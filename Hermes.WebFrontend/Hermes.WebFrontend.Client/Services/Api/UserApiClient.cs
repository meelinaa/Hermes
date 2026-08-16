using System.Net.Http.Json;
using System.Text.Json;
using Hermes.WebFrontend.Client.ApiModels;

namespace Hermes.WebFrontend.Client.Services.Api;

/// <summary>
/// Implements typed HTTP communication with user management and verification API endpoints.
/// </summary>
public sealed class UserApiClient(HttpClient http) : IUserApiClient
{
    private static readonly JsonSerializerOptions _json = JsonSerializerOptions.Web;

    /// <summary>
    /// Retrieves user profile information by numeric user ID.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing user profile details on success.</returns>
    public async Task<ApiResult<UserScopeDto>> GetUserProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync($"api/v1/users/{userId}", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var (msg, type, validation) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                return ApiResult<UserScopeDto>.Failure(msg, type, (int)response.StatusCode, validation);
            }

            UserScopeDto? data = await response.Content.ReadFromJsonAsync<UserScopeDto>(_json, cancellationToken).ConfigureAwait(false);
            if (data is null)
                return ApiResult<UserScopeDto>.Failure("Ungültige Antwort vom Server empfangen.", statusCode: (int)response.StatusCode);

            return ApiResult<UserScopeDto>.Success(data);
        }
        catch (Exception ex)
        {
            return ApiResult<UserScopeDto>.Failure($"Verbindung zum Server fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates user profile details or password.
    /// </summary>
    /// <param name="request">The update payload containing name, email, and optional passwords.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing the updated user details on success.</returns>
    public async Task<ApiResult<UserScopeDto>> UpdateUserAsync(UserProfileUpdateRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await http.PutAsJsonAsync($"api/v1/users/{request.Id}", request, _json, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var (msg, type, validation) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                return ApiResult<UserScopeDto>.Failure(msg, type, (int)response.StatusCode, validation);
            }

            UserScopeDto? data = await response.Content.ReadFromJsonAsync<UserScopeDto>(_json, cancellationToken).ConfigureAwait(false);
            return data != null
                ? ApiResult<UserScopeDto>.Success(data)
                : ApiResult<UserScopeDto>.Failure("Ungültige Antwort vom Server empfangen.", statusCode: (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            return ApiResult<UserScopeDto>.Failure($"Speichern fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Requests dispatch of a verification OTP email.
    /// </summary>
    /// <param name="userId">The ID of the user requesting verification.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing email dispatch information on success.</returns>
    public async Task<ApiResult<SendVerificationMailResponseDto>> SendEmailVerificationCodeAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await http.PostAsync($"api/v1/users/{userId}/email-verifications", null, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var (msg, type, validation) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                return ApiResult<SendVerificationMailResponseDto>.Failure(msg, type, (int)response.StatusCode, validation);
            }

            SendVerificationMailResponseDto? data = await response.Content.ReadFromJsonAsync<SendVerificationMailResponseDto>(_json, cancellationToken).ConfigureAwait(false);
            return data != null
                ? ApiResult<SendVerificationMailResponseDto>.Success(data)
                : ApiResult<SendVerificationMailResponseDto>.Failure("Ungültige Antwort vom Server empfangen.", statusCode: (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            return ApiResult<SendVerificationMailResponseDto>.Failure($"Versand fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies the OTP code submitted by the user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="code">The OTP verification code.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing updated user details on success.</returns>
    public async Task<ApiResult<UserScopeDto>> ConfirmEmailVerificationCodeAsync(int userId, string code, CancellationToken cancellationToken = default)
    {
        try
        {
            var body = new UserVerificationCodeRequestDto(userId, code);
            using HttpResponseMessage response = await http.PostAsJsonAsync($"api/v1/users/{userId}/email-verifications/confirmations", body, _json, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var (msg, type, validation) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                return ApiResult<UserScopeDto>.Failure(msg, type, (int)response.StatusCode, validation);
            }

            UserScopeDto? data = await response.Content.ReadFromJsonAsync<UserScopeDto>(_json, cancellationToken).ConfigureAwait(false);
            return data != null
                ? ApiResult<UserScopeDto>.Success(data)
                : ApiResult<UserScopeDto>.Failure("Ungültige Antwort vom Server empfangen.", statusCode: (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            return ApiResult<UserScopeDto>.Failure($"Verifizierung fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Permanently deletes the user account.
    /// </summary>
    /// <param name="userId">The ID of the user to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result indicating success or failure.</returns>
    public async Task<ApiResult> DeleteAccountAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await http.DeleteAsync($"api/v1/users/{userId}", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var (msg, type, validation) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                return ApiResult.Failure(msg, type, (int)response.StatusCode, validation);
            }

            return ApiResult.Success();
        }
        catch (Exception ex)
        {
            return ApiResult.Failure($"Löschen fehlgeschlagen: {ex.Message}");
        }
    }
}
