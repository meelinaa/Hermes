using System.Collections.Concurrent;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Distributed;

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
    IUserVerificationService verificationService,
    IDistributedCache? distributedCache = null,
    TimeProvider? timeProvider = null) : ControllerBase
{
    private static readonly TimeSpan _verificationMailCooldown = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<int, DateTimeOffset> _lastVerificationMailByUserId = new();

    /// <summary>
    /// Creates a new user identity and provisions their initial data structures.
    /// Acts as the public entry point for new customers to join the platform.
    /// Returns 201 Created with the Location header pointing to the created user resource,
    /// or 409 Conflict if a user with the requested email already exists.
    /// </summary>
    /// <param name="request">The registration payload containing name, email, and password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A 201 Created result containing the created user profile.</returns>
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<UserResponseDto>> SetNewUser([FromBody] RegisterUserRequestDto request, CancellationToken cancellationToken)
    {
        Result<UserScopeDto> registerResult = await authService.RegisterUserAsync(request, cancellationToken).ConfigureAwait(false);
        if (registerResult.IsFailed)
            return this.ToProblemResult(registerResult.Errors.First());

        UserResponseDto response = registerResult.Value.ToUserResponse();
        return CreatedAtAction(nameof(GetUserById), new { id = registerResult.Value.UserId }, response);
    }

    /// <summary>
    /// Applies changes to a user's master record. 
    /// Automatically revokes email verification status if the email address is changed.
    /// Enforces IDOR authorization matching caller identity against route parameter id.
    /// </summary>
    /// <param name="id">The target user ID in the URL path.</param>
    /// <param name="request">The updated profile payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user profile response.</returns>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserResponseDto>> UpdateUser(int id, [FromBody] UserProfileUpdateRequestDto request, CancellationToken cancellationToken)
    {
        if (id != request.Id)
            request.Id = id;

        if (this.WhenCannotAccessUser(id) is { } denied)
            return denied;

        Result updateResult = await authService.UpdateUserAsync(id, request.Name, request.Email, request.NewPassword, request.CurrentPassword, cancellationToken).ConfigureAwait(false);
        if (updateResult.IsFailed)
            return this.ToProblemResult(updateResult.Errors.First());

        Result<UserScopeDto> updatedResult = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        return updatedResult.IsFailed ? this.ToProblemResult(updatedResult.Errors.First()) : Ok(updatedResult.Value.ToUserResponse());
    }

    /// <summary>
    /// Permanently removes a user and their cascaded data (e.g. subscriptions, tokens) from the system.
    /// Satisfies right-to-be-forgotten GDPR requirements and returns 204 No Content upon success.
    /// </summary>
    /// <param name="id">The target user ID in the URL path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A 204 No Content result.</returns>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        Result<UserScopeDto> userResult = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        if (userResult.IsFailed)
            return this.ToProblemResult(userResult.Errors.First());

        await userService.DeleteUserAsync(userResult.Value, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Fetches the user's current state to synchronize client applications.
    /// Typically called upon application startup to restore session context.
    /// </summary>
    /// <param name="id">The user ID in the route path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user profile response DTO.</returns>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_ID)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserResponseDto>> GetUserById(int id, CancellationToken cancellationToken)
    {
        Result<UserScopeDto> userResult = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        return userResult.IsFailed ? this.ToProblemResult(userResult.Errors.First()) : Ok(userResult.Value.ToUserResponse());
    }

    /// <summary>
    /// Looks up a user account by email query parameter for the currently authenticated caller.
    /// Returns 404 if the user is not found, or 403 Forbidden if the requested email belongs to a different user.
    /// </summary>
    /// <param name="email">The email address query string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching user profile response.</returns>
    [HttpGet]
    public async Task<ActionResult<UserResponseDto>> GetUserByEmail([FromQuery] string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return this.BadRequestProblem("Query parameter 'email' is required.");

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
    /// Returns 202 Accepted when the verification email delivery task is enqueued.
    /// Protects against spam by enforcing a strict in-memory cooldown period per user.
    /// </summary>
    /// <param name="id">The target user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A 202 Accepted response with verification dispatch metadata.</returns>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_ID)]
    [EnableRateLimiting("VerifyMailPolicy")]
    [HttpPost("{id:int}/email-verifications")]
    public async Task<ActionResult<SendVerificationMailResponseDto>> SendVerificationMail(int id, CancellationToken cancellationToken)
    {
        Result<UserScopeDto> userResult = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        if (userResult.IsFailed || string.IsNullOrWhiteSpace(userResult.Value.Email))
            return this.NotFoundProblem();

        if (await TryGetVerificationMailCooldownResponseAsync(id, cancellationToken).ConfigureAwait(false) is { } cooldownResult)
            return cooldownResult;

        Result sendResult = await verificationService.SendVerificationMailAsync(userResult.Value.Email, cancellationToken).ConfigureAwait(false);
        if (sendResult.IsFailed)
            return this.ToProblemResult(sendResult.Errors.First());

        await RegisterVerificationMailSendAsync(id, cancellationToken).ConfigureAwait(false);
        return Accepted(new SendVerificationMailResponseDto(id, userResult.Value.Email));
    }

    /// <summary>
    /// Consumes the OTP provided via email to mark the account as verified.
    /// Unlocks platform features that require a confirmed email address.
    /// </summary>
    /// <param name="id">The target user ID in the route path.</param>
    /// <param name="request">The verification code payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user profile reflecting the verified email status.</returns>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_ID)]
    [EnableRateLimiting("VerifyCodePolicy")]
    [HttpPost("{id:int}/email-verifications/confirmations")]
    public async Task<ActionResult<UserResponseDto>> CheckVerificationCode(int id, [FromBody] UserVerificationCodeRequestDto request, CancellationToken cancellationToken)
    {
        if (request.UserId != id)
            request.UserId = id;

        if (this.WhenCannotAccessUser(id) is { } denied)
            return denied;

        Result checkResult = await verificationService.CheckVerificationCodeAsync(new UserId(id), request.Code, cancellationToken).ConfigureAwait(false);
        if (checkResult.IsFailed)
            return this.ToProblemResult(checkResult.Errors.First());

        Result<UserScopeDto> refreshedResult = await userService.GetUserByIdAsync(new UserId(id), cancellationToken).ConfigureAwait(false);
        return refreshedResult.IsFailed ? this.ToProblemResult(refreshedResult.Errors.First()) : Ok(refreshedResult.Value.ToUserResponse());
    }

    /// <summary>
    /// Evaluates the time elapsed since the last OTP dispatch across distributed instances to prevent SMTP abuse.
    /// Returns a Retry-After header hint if the cooldown is still active.
    /// </summary>
    private async Task<ActionResult?> TryGetVerificationMailCooldownResponseAsync(int userId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        string cacheKey = $"cooldown:email-verify:{userId}";

        if (distributedCache is not null)
        {
            string? cachedTimestamp = await distributedCache.GetStringAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(cachedTimestamp) && long.TryParse(cachedTimestamp, out long unixSeconds))
            {
                DateTimeOffset lastSentAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                TimeSpan elapsed = now - lastSentAt;
                if (elapsed < _verificationMailCooldown)
                {
                    int remainingSeconds = Math.Max(1, (int)Math.Ceiling((_verificationMailCooldown - elapsed).TotalSeconds));
                    Response.Headers.RetryAfter = remainingSeconds.ToString();
                    return this.BadRequestProblem($"Please wait {remainingSeconds}s before requesting another verification email.");
                }
            }
        }
        else if (_lastVerificationMailByUserId.TryGetValue(userId, out DateTimeOffset lastSentAt))
        {
            TimeSpan elapsed = now - lastSentAt;
            if (elapsed < _verificationMailCooldown)
            {
                int remainingSeconds = Math.Max(1, (int)Math.Ceiling((_verificationMailCooldown - elapsed).TotalSeconds));
                Response.Headers.RetryAfter = remainingSeconds.ToString();
                return this.BadRequestProblem($"Please wait {remainingSeconds}s before requesting another verification email.");
            }
        }

        return null;
    }

    /// <summary>
    /// Updates the distributed cache and in-memory fallback with the current UTC timestamp after a successful dispatch.
    /// </summary>
    private async Task RegisterVerificationMailSendAsync(int userId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        string cacheKey = $"cooldown:email-verify:{userId}";

        if (distributedCache is not null)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _verificationMailCooldown
            };
            await distributedCache.SetStringAsync(cacheKey, now.ToUnixTimeSeconds().ToString(), options, cancellationToken).ConfigureAwait(false);
        }

        _lastVerificationMailByUserId[userId] = now;
    }
}
