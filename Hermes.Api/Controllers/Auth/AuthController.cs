using Hermes.Api.Http;
using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.Security;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services.Security;
using Hermes.Domain.ValueObjects;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hermes.Api.Controllers.Auth;

/// <summary>
/// Exposes authentication flows to client applications, enabling session establishment, 
/// token rotation, and secure logout.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(IUserAuthenticationService authService) : ControllerBase
{
    /// <summary>
    /// Establishes a new authenticated session for the user upon successful credential verification.
    /// Uses rate limiting to mitigate credential stuffing and brute-force attacks.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("AuthLoginPolicy")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request,
        [FromServices] IAuthTokenService authTokens,
        CancellationToken cancellationToken)
    {
        Result<LoginResultDto> loginResult = await authService.LoginAsync(request.NameOrEmail, request.Password, cancellationToken).ConfigureAwait(false);
        if (loginResult.IsFailed)
            return this.UnauthorizedProblem(loginResult.Errors.First().Message);

        LoginResultDto result = loginResult.Value;
        if (!result.Success)
            return this.UnauthorizedProblem(result.ErrorMessage);

        AuthTokensResultDto tokens = await authTokens.IssueTokensAsync(new UserId(result.UserId!.Value), result.Email, result.Name, cancellationToken).ConfigureAwait(false);
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
    /// Extends an active user session by exchanging a valid refresh token for a fresh JWT.
    /// Protects against token theft via strict rotation policies in the underlying service layer.
    /// </summary>
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
    /// Terminates the current session by revoking the active refresh token, preventing further rotation.
    /// If no specific token is provided, all sessions for the user are immediately invalidated (e.g. for security lockdown).
    /// </summary>
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
            await authTokens.RevokeAllForUserAsync(new UserId(userId), cancellationToken).ConfigureAwait(false);
            return NoContent();
        }

        bool ok = await authTokens.TryRevokeRefreshForUserAsync(body.RefreshToken, new UserId(userId), cancellationToken).ConfigureAwait(false);
        if (!ok)
            return this.UnauthorizedProblem("Invalid or expired refresh token.");

        return NoContent();
    }
}
