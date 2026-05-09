using FluentValidation;
using FluentValidation.Results;
using Hermes.Api.Http;
using Hermes.Api.Validation;
using Hermes.Application.Models;
using Hermes.Application.Models.Login;
using Hermes.Application.Security;
using Hermes.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hermes.Api.Controllers;

/// <summary>
/// Authentication endpoints: password login issues JWT + refresh; refresh exchanges a valid refresh for a new pair;
/// logout revokes refresh row(s). Protected routes use <c>Authorization: Bearer &lt;accessToken&gt;</c>.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(IUserService userService) : ControllerBase
{
    /// <summary>Login with display name or email plus plain password (BCrypt verify).</summary>
    /// <remarks>
    /// <b>POST</b> <c>api/v1/auth/login</c> — Body (application/json):
    /// <para>By email:</para>
    /// <code>
    /// {
    ///   "nameOrEmail": "max@example.com",
    ///   "password": "plain-password"
    /// }
    /// </code>
    /// <para>By display name:</para>
    /// <code>
    /// {
    ///   "nameOrEmail": "Max Mustermann",
    ///   "password": "plain-password"
    /// }
    /// </code>
    /// <para>Returns access and refresh tokens; store refresh securely. Access token is short-lived; use <c>POST …/refresh</c> to renew.</para>
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("AuthLoginPolicy")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] IValidator<LoginRequest> loginValidator,
        [FromServices] IAuthTokenService authTokens,
        CancellationToken cancellationToken)
    {
        ValidationResult fv = await loginValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!fv.IsValid)
            return fv.ToValidationProblem(this);

        LoginResult result = await userService.LoginAsync(request.NameOrEmail, request.Password, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            return this.UnauthorizedProblem(result.ErrorMessage);

        AuthTokensResult tokens = await authTokens.IssueTokensAsync(result.UserId!.Value, result.Email, result.Name, cancellationToken).ConfigureAwait(false);
        LoginResponse body = new(
            Success: true,
            UserId: result.UserId!.Value,
            AccessToken: tokens.AccessToken,
            TokenType: "Bearer",
            ExpiresAt: tokens.AccessTokenExpiresAtUtc,
            RefreshToken: tokens.RefreshToken,
            RefreshTokenExpiresAt: tokens.RefreshTokenExpiresAtUtc);
        return Ok(body);
    }

    /// <summary>Exchange a valid refresh token for a new access + refresh pair (rotation).</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("AuthRefreshPolicy")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        [FromServices] IValidator<RefreshRequest> refreshValidator,
        [FromServices] IAuthTokenService authTokens,
        CancellationToken cancellationToken)
    {
        ValidationResult fv = await refreshValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!fv.IsValid)
            return fv.ToValidationProblem(this);

        AuthTokensResult? next = await authTokens.RotateAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);
        if (next is null)
            return this.UnauthorizedProblem("Invalid or expired refresh token.");

        RefreshResponse body = new(
            Success: true,
            AccessToken: next.AccessToken,
            TokenType: "Bearer",
            ExpiresAt: next.AccessTokenExpiresAtUtc,
            RefreshToken: next.RefreshToken,
            RefreshTokenExpiresAt: next.RefreshTokenExpiresAtUtc);
        return Ok(body);
    }

    /// <summary>
    /// Revokes refresh token(s). With body <c>{ "refreshToken": "…" }</c> revokes that session if it belongs to the caller;
    /// with empty body revokes all refresh tokens for the caller (logout everywhere).
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest? body,
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
