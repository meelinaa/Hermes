using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.Model;

namespace Hermes.WebFrontend.Client.Services.Api;

/// <summary>
/// Strongly typed client interface for authentication and registration endpoints.
/// </summary>
public interface IAuthApiClient
{
    /// <summary>
    /// Authenticates user credentials via the API and returns access and refresh tokens.
    /// </summary>
    /// <param name="request">The login credentials.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing token information on success.</returns>
    Task<ApiResult<LoginResponseDto>> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a new user registration request to the API.
    /// </summary>
    /// <param name="request">The registration payload.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>An API result containing registered user details on success.</returns>
    Task<ApiResult<UserScopeDto>> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
}
