using System.Net.Http.Json;
using System.Text.Json;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.Model;

namespace Hermes.WebFrontend.Client.Services.Api;

/// <summary>
/// Implements typed HTTP communication with authentication and registration API endpoints.
/// </summary>
public sealed class AuthApiClient(HttpClient http) : IAuthApiClient
{
    private static readonly JsonSerializerOptions _json = JsonSerializerOptions.Web;

    /// <summary>
    /// Authenticates user credentials via the API and returns access and refresh tokens.
    /// </summary>
    /// <param name="request">The login credentials.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing token information on success.</returns>
    public async Task<ApiResult<LoginResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync("api/v1/auth/login", request, _json, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var (msg, type, validation) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                return ApiResult<LoginResponseDto>.Failure(msg, type, (int)response.StatusCode, validation);
            }

            LoginResponseDto? data = await response.Content.ReadFromJsonAsync<LoginResponseDto>(_json, cancellationToken).ConfigureAwait(false);
            if (data is null)
                return ApiResult<LoginResponseDto>.Failure("Ungültige Antwort vom Server empfangen.", statusCode: (int)response.StatusCode);

            return ApiResult<LoginResponseDto>.Success(data);
        }
        catch (Exception ex)
        {
            return ApiResult<LoginResponseDto>.Failure($"Verbindung zum Server fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Dispatches a new user registration request to the API.
    /// </summary>
    /// <param name="request">The registration payload.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing registered user details on success.</returns>
    public async Task<ApiResult<UserScopeDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await http.PostAsJsonAsync("api/v1/users", request, _json, cancellationToken).ConfigureAwait(false);
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
}
