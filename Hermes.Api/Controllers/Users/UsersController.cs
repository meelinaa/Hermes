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
using FluentResults;
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
    /// Returns 409 Conflict if a user with the requested email already exists.
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<UserResponseDto>> SetNewUser([FromBody] RegisterUserRequestDto request, CancellationToken cancellationToken)
    {
        Result<UserScopeDto> registerResult = await authService.RegisterUserAsync(request, cancellationToken).ConfigureAwait(false);
        if (registerResult.IsFailed)
        {
            string errorMessage = registerResult.Errors.First().Message;
            if (errorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return this.ConflictProblem(errorMessage);

            return this.BadRequestProblem(errorMessage);
        }

        return Ok(registerResult.Value.ToUserResponse());
    }

    /// <summary>
    /// Applies changes to a user's master record. 
    /// Automatically revokes email verification status if the email address is changed.
    /// Returns 400 with custom problem type when current password verification fails.
    /// </summary>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPut]
    public async Task<ActionResult<UserResponseDto>> UpdateUser([FromBody] UserProfileUpdateRequestDto request, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(request.Id) is { } denied)
            return denied;

        Result updateResult = await authService.UpdateUserAsync(request.Id, request.Name, request.Email, request.NewPassword, request.CurrentPassword, cancellationToken).ConfigureAwait(false);
        if (updateResult.IsFailed)
        {
            string errorMessage = updateResult.Errors.First().Message;
            if (errorMessage.Contains("Current password", StringComparison.OrdinalIgnoreCase))
                return this.WrongCurrentPasswordProblem(errorMessage);

            return this.BadRequestProblem(errorMessage);
        }

        Result<UserScopeDto> updatedResult = await userService.GetUserByIdAsync(new UserId(request.Id), cancellationToken).ConfigureAwait(false);
        return updatedResult.IsFailed ? this.NotFoundProblem() : Ok(updatedResult.Value.ToUserResponse());
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
        Result<UserScopeDto> userResult = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        if (userResult.IsFailed)
            return this.NotFoundProblem();

        await userService.DeleteUserAsync(userResult.Value, cancellationToken).ConfigureAwait(false);
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
        Result<UserScopeDto> userResult = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        return userResult.IsFailed ? this.NotFoundProblem() : Ok(userResult.Value.ToUserResponse());
    }

    /// <summary>
    /// Looks up a user account by email address for the currently authenticated caller.
    /// Returns 404 if the user is not found, or 403 Forbidden if the requested email belongs to a different user.
    /// </summary>
    [HttpGet("by-email/{email}")]
    public async Task<ActionResult<UserResponseDto>> GetUserByEmail(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return this.BadRequestProblem("Path segment 'email' is required.");

        if (!this.TryGetCurrentUserId(out int currentUserId))
            return this.UnauthorizedProblem("Missing user identity.");

        Result<UserScopeDto> userResult = await userService.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (userResult.IsFailed)
            return this.NotFoundProblem();

        if (userResult.Value.UserId != currentUserId)
            return this.ForbiddenProblem();

        return Ok(userResult.Value.ToUserResponse());
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
        Result<UserScopeDto> userResult = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        if (userResult.IsFailed || string.IsNullOrWhiteSpace(userResult.Value.Email))
            return this.NotFoundProblem();

        if (TryGetVerificationMailCooldownResponse(id) is { } cooldownResult)
            return cooldownResult;

        await verificationService.SendVerificationMailAsync(userResult.Value.Email, cancellationToken).ConfigureAwait(false);
        RegisterVerificationMailSend(id);
        return Ok(new SendVerificationMailResponseDto(id, userResult.Value.Email));
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

        Result<UserScopeDto> refreshedResult = await userService.GetUserByIdAsync(new UserId(request.UserId), cancellationToken).ConfigureAwait(false);
        return refreshedResult.IsFailed ? this.NotFoundProblem() : Ok(refreshedResult.Value.ToUserResponse());
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
