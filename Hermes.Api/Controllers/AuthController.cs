using Hermes.Api.Http;
using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.Security;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hermes.Api.Controllers;

/// <summary>
/// Handles authentication endpoints such as login, refresh tokens, and logout.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(IUserAuthenticationService authService) : ControllerBase
{
    /// <summary>
    /// Processes a user login request, validating the credentials and returning access/refresh tokens.
    /// Validation is handled automatically by the global filters.
    /// </summary>
    /// <param name="request">The login payload containing credentials.</param>
    /// <param name="authTokens">The token service used to issue JWTs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An action result containing tokens or an unauthorized problem.</returns>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("AuthLoginPolicy")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request,
        [FromServices] IAuthTokenService authTokens,
        CancellationToken cancellationToken)
    {
        LoginResultDto result = await authService.LoginAsync(request.NameOrEmail, request.Password, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            return this.UnauthorizedProblem(result.ErrorMessage);

        AuthTokensResultDto tokens = await authTokens.IssueTokensAsync(result.UserId!.Value, result.Email, result.Name, cancellationToken).ConfigureAwait(false);
        LoginResponseDto body = new(
            Success: true,
            UserId: result.UserId!.Value,
            AccessToken: tokens.AccessToken,
            TokenType: "Bearer",
            ExpiresAt: tokens.AccessTokenExpiresAtUtc,
            RefreshToken: tokens.RefreshToken,
            RefreshTokenExpiresAt: tokens.RefreshTokenExpiresAtUtc);
        return Ok(body);
    }

    /// <summary>
    /// Rotates the refresh token to extend the user session and issues a new access token.
    /// Validation is handled automatically by the global filters.
    /// </summary>
    /// <param name="request">The refresh payload containing the current refresh token.</param>
    /// <param name="authTokens">The token service used to rotate the tokens.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An action result containing rotated tokens or an unauthorized problem.</returns>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("AuthRefreshPolicy")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequestDto request,
        [FromServices] IAuthTokenService authTokens,
        CancellationToken cancellationToken)
    {
        AuthTokensResultDto? next = await authTokens.RotateAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);
        if (next is null)
            return this.UnauthorizedProblem("Invalid or expired refresh token.");

        RefreshResponseDto body = new(
            Success: true,
            AccessToken: next.AccessToken,
            TokenType: "Bearer",
            ExpiresAt: next.AccessTokenExpiresAtUtc,
            RefreshToken: next.RefreshToken,
            RefreshTokenExpiresAt: next.RefreshTokenExpiresAtUtc);
        return Ok(body);
    }

    /// <summary>
    /// Logs out the user by revoking the supplied refresh token (or all tokens if empty).
    /// </summary>
    /// <param name="body">The optional logout request containing the refresh token to revoke.</param>
    /// <param name="authTokens">The token service used to revoke session state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A no content result or unauthorized if verification fails.</returns>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequestDto? body,
        [FromServices] IAuthTokenService authTokens,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetCurrentUserId(out int userId))
            return this.UnauthorizedProblem("Missing user identity.");

        if (string.IsNullOrWhiteSpace(body?.RefreshToken))
        {
            await authTokens.RevokeAllForUserAsync(userId, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }

        bool ok = await authTokens.TryRevokeRefreshForUserAsync(body.RefreshToken, userId, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return this.UnauthorizedProblem("Invalid or expired refresh token.");

        return NoContent();
    }
}
