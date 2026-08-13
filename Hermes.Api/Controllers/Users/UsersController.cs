using System.Collections.Concurrent;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using Hermes.Api.Constants;
using Hermes.Api.Http;
using Hermes.Api.Mapping.Users;
using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Inbound;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Api.Controllers.Users;

/// <summary>
/// Provides lifecycle management for user accounts. 
/// Handles self-registration, profile updates, account deletion, and email address verification workflows.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/users")]
public class UsersController(
    IUserService userService,
    IUserAuthenticationService authService,
    IUserVerificationService verificationService) : ControllerBase
{
    private static readonly TimeSpan _verificationMailCooldown = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<int, DateTimeOffset> _lastVerificationMailByUserId = new();

    /// <summary>
    /// Creates a new user identity and provisions their initial data structures.
    /// Acts as the public entry point for new customers to join the platform.
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<UserResponseDto>> SetNewUser([FromBody] RegisterUserRequestDto request, CancellationToken cancellationToken)
    {
        UserScopeDto userScope = await authService.RegisterUserAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(userScope.ToUserResponse());
    }

    /// <summary>
    /// Applies changes to a user's master record. 
    /// Automatically revokes email verification status if the email address is changed.
    /// </summary>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPut]
    public async Task<ActionResult<UserResponseDto>> UpdateUser([FromBody] UserProfileUpdateRequestDto request, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(request.Id) is { } denied)
            return denied;

        User user = new()
        {
            Id = new UserId(request.Id),
            Name = request.Name,
            Email = request.Email,
            PasswordHash = request.NewPassword
        };

        await authService.UpdateUserAsync(user, request.CurrentPassword, cancellationToken).ConfigureAwait(false);

        UserScopeDto? updated = await userService.GetUserByIdAsync(new UserId(request.Id), cancellationToken).ConfigureAwait(false);
        return updated is null ? this.NotFoundProblem() : Ok(updated.ToUserResponse());
    }

    /// <summary>
    /// Permanently removes a user and their cascaded data (e.g. subscriptions, tokens) from the system.
    /// Satisfies right-to-be-forgotten GDPR requirements.
    /// </summary>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        UserScopeDto? user = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        if (user is null)
            return this.NotFoundProblem();

        await userService.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>
    /// Fetches the user's current state to synchronize client applications.
    /// Typically called upon application startup to restore session context.
    /// </summary>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_ID)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserResponseDto>> GetUserById(int id, CancellationToken cancellationToken)
    {
        UserScopeDto? user = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        return user is null ? this.NotFoundProblem() : Ok(user.ToUserResponse());
    }

    /// <summary>
    /// Looks up a user account by email address.
    /// Used during administrative flows or invite-acceptance processes where the internal ID is not yet known.
    /// </summary>
    [HttpGet("by-email/{email}")]
    public async Task<ActionResult<UserResponseDto>> GetUserByEmail(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return this.BadRequestProblem("Path segment 'email' is required.");

        UserScopeDto? user = await userService.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return this.NotFoundProblem();

        if (this.WhenCannotAccessUser(user.UserId) is { } denied)
            return denied;

        return Ok(user.ToUserResponse());
    }

    /// <summary>
    /// Dispatches an email containing a 6-digit OTP to prove domain ownership.
    /// Protects against spam by enforcing a strict in-memory cooldown period per user.
    /// </summary>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_ID)]
    [EnableRateLimiting("VerifyMailPolicy")]
    [HttpPost("{id:int}/verify")]
    public async Task<ActionResult<SendVerificationMailResponseDto>> SendVerificationMail(int id, CancellationToken cancellationToken)
    {
        UserScopeDto? user = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return this.NotFoundProblem();

        if (TryGetVerificationMailCooldownResponse(id) is { } cooldownResult)
            return cooldownResult;

        await verificationService.SendVerificationMailAsync(user.Email, cancellationToken).ConfigureAwait(false);
        RegisterVerificationMailSend(id);
        return Ok(new SendVerificationMailResponseDto(id, user.Email));
    }

    /// <summary>
    /// Consumes the OTP provided via email to mark the account as verified.
    /// Unlocks platform features that require a confirmed email address.
    /// </summary>
    [EnableRateLimiting("VerifyCodePolicy")]
    [HttpPost("verify/code")]
    public async Task<ActionResult<UserResponseDto>> CheckVerificationCode([FromBody] UserVerificationCodeRequestDto request, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(request.UserId) is { } denied)
            return denied;

        await verificationService.CheckVerificationCodeAsync(new UserId(request.UserId), request.Code, cancellationToken).ConfigureAwait(false);

        UserScopeDto? refreshed = await userService.GetUserByIdAsync(new UserId(request.UserId), cancellationToken).ConfigureAwait(false);
        return refreshed is null ? this.NotFoundProblem() : Ok(refreshed.ToUserResponse());
    }

    /// <summary>
    /// Evaluates the time elapsed since the last OTP dispatch to prevent SMTP abuse.
    /// Returns a Retry-After header hint if the cooldown is still active.
    /// </summary>
    private ActionResult? TryGetVerificationMailCooldownResponse(int userId)
    {
        if (!_lastVerificationMailByUserId.TryGetValue(userId, out DateTimeOffset lastSentAt))
            return null;

        TimeSpan elapsed = DateTimeOffset.UtcNow - lastSentAt;
        if (elapsed >= _verificationMailCooldown)
            return null;

        int remainingSeconds = Math.Max(1, (int)Math.Ceiling((_verificationMailCooldown - elapsed).TotalSeconds));
        Response.Headers.RetryAfter = remainingSeconds.ToString();
        return this.BadRequestProblem($"Please wait {remainingSeconds}s before requesting another verification email.");
    }

    /// <summary>
    /// Updates the in-memory rate-limiting dictionary with the current UTC timestamp after a successful dispatch.
    /// </summary>
    private static void RegisterVerificationMailSend(int userId)
        => _lastVerificationMailByUserId[userId] = DateTimeOffset.UtcNow;
}
