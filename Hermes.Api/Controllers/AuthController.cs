using FluentValidation;
using Hermes.Api.Http;
using Hermes.Api.Validation;
using Hermes.Application.Models;
using Hermes.Application.Security;
using Hermes.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] IValidator<LoginRequest> loginValidator,
        [FromServices] IAuthTokenService authTokens,
        CancellationToken cancellationToken)
    {
        var fv = await loginValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!fv.IsValid)
            return fv.ToValidationProblem(this);

        // Credential check only; no tokens until this succeeds.
        var result = await userService.LoginAsync(request.NameOrEmail, request.Password, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            return this.UnauthorizedProblem(result.ErrorMessage);

        // Persist refresh (hash) and return JWT + plain refresh once.
        var tokens = await authTokens.IssueTokensAsync(result.UserId!.Value, result.Email, result.Name, cancellationToken).ConfigureAwait(false);
        return Ok(new
        {
            success = true,
            userId = result.UserId,
            accessToken = tokens.AccessToken,
            tokenType = "Bearer",
            expiresAt = tokens.AccessTokenExpiresAtUtc,
            refreshToken = tokens.RefreshToken,
            refreshTokenExpiresAt = tokens.RefreshTokenExpiresAtUtc
        });
    }

    /// <summary>Exchange a valid refresh token for a new access + refresh pair (rotation).</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshRequest request,
        [FromServices] IValidator<RefreshRequest> refreshValidator,
        [FromServices] IAuthTokenService authTokens,
        CancellationToken cancellationToken)
    {
        var fv = await refreshValidator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!fv.IsValid)
            return fv.ToValidationProblem(this);

        // No Bearer header required: the refresh body proves the session. Old refresh is revoked server-side.
        var next = await authTokens.RotateAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);
        if (next is null)
            return this.UnauthorizedProblem("Invalid or expired refresh token.");

        return Ok(new
        {
            success = true,
            accessToken = next.AccessToken,
            tokenType = "Bearer",
            expiresAt = next.AccessTokenExpiresAtUtc,
            refreshToken = next.RefreshToken,
            refreshTokenExpiresAt = next.RefreshTokenExpiresAtUtc
        });
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
        // User id comes from the validated JWT, not from the client body (prevents cross-user revoke).
        if (!this.TryGetCurrentUserId(out var userId))
            return this.UnauthorizedProblem("Missing user identity.");

        if (string.IsNullOrWhiteSpace(body?.RefreshToken))
        {
            await authTokens.RevokeAllForUserAsync(userId, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }

        var ok = await authTokens.TryRevokeRefreshForUserAsync(body.RefreshToken, userId, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return this.BadRequestProblem("Invalid or expired refresh token.");

        return NoContent();
    }
}
