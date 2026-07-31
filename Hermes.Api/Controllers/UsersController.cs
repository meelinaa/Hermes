using Hermes.Api.Authorization;
using Hermes.Api.Http;
using Hermes.Api.Mapping;
using Hermes.Application.Models.User;
using Hermes.Domain.Entities;
using Hermes.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Collections.Concurrent;
using Hermes.Application.DTOs;

namespace Hermes.Api.Controllers;

/// <summary>
/// Controller for managing user profiles, registrations, and account verification actions.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    private static readonly TimeSpan _verificationMailCooldown = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<int, DateTimeOffset> _lastVerificationMailByUserId = new();

    /// <summary>
    /// Registers a new user with the specified credentials.
    /// Validation is handled automatically by the global filters.
    /// </summary>
    /// <param name="request">The registration payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A response containing the registered user details.</returns>
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<UserResponse>> SetNewUser([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        UserScope userScope = await userService.RegisterUserAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(userScope.ToUserResponse());
    }

    /// <summary>
    /// Updates the profile (name, email, or password) of the authenticated user.
    /// Validation is handled automatically by the global filters.
    /// Exceptions are caught by global middleware.
    /// </summary>
    /// <param name="request">The profile update payload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated user profile response.</returns>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPut]
    public async Task<ActionResult<UserResponse>> UpdateUser([FromBody] UserProfileUpdateRequest request, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(request.Id) is { } denied)
            return denied;

        User user = new()
        {
            Id = request.Id,
            Name = request.Name,
            Email = request.Email,
            PasswordHash = request.NewPassword
        };

        await userService.UpdateUserAsync(user, request.CurrentPassword, cancellationToken).ConfigureAwait(false);

        UserScope? updated = await userService.GetUserByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        return updated is null ? this.NotFoundProblem() : Ok(updated.ToUserResponse());
    }

    /// <summary>
    /// Deletes a user profile and all associated data.
    /// </summary>
    /// <param name="id">The ID of the user to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An OK result if deleted successfully.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        UserScope? user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return this.NotFoundProblem();

        await userService.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>
    /// Retrieves user profile details by ID.
    /// </summary>
    /// <param name="id">The ID of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user details.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_ID)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserResponse>> GetUserById(int id, CancellationToken cancellationToken)
    {
        UserScope? user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return user is null ? this.NotFoundProblem() : Ok(user.ToUserResponse());
    }

    /// <summary>
    /// Retrieves user profile details by their email address.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user details.</returns>
    [HttpGet("by-email/{email}")]
    public async Task<ActionResult<UserResponse>> GetUserByEmail(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return this.BadRequestProblem("Path segment 'email' is required.");

        UserScope? user = await userService.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return this.NotFoundProblem();

        if (this.WhenCannotAccessUser(user.UserId) is { } denied)
            return denied;

        return Ok(user.ToUserResponse());
    }

    /// <summary>
    /// Requests a new verification email for the specified user ID (subject to a cooldown limit).
    /// </summary>
    /// <param name="id">The ID of the user to send the verification email to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result status of the verification email request.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_ID)]
    [EnableRateLimiting("VerifyMailPolicy")]
    [HttpPost("{id:int}/verify")]
    public async Task<ActionResult<SendVerificationMailResponse>> SendVerificationMail(int id, CancellationToken cancellationToken)
    {
        UserScope? user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return this.NotFoundProblem();

        if (TryGetVerificationMailCooldownResponse(id) is { } cooldownResult)
            return cooldownResult;

        await userService.SendVerificationMailAsync(user.Email, cancellationToken).ConfigureAwait(false);
        RegisterVerificationMailSend(id);
        return Ok(new SendVerificationMailResponse(id, user.Email));
    }

    /// <summary>
    /// Verifies the user's email address by checking the supplied numeric verification code.
    /// Validation is handled automatically by the global filters.
    /// </summary>
    /// <param name="request">The verification payload containing the code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated user details if verification was successful.</returns>
    [EnableRateLimiting("VerifyCodePolicy")]
    [HttpPost("verify/code")]
    public async Task<ActionResult<UserResponse>> CheckVerificationCode([FromBody] UserVerificationCodeRequest request, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(request.UserId) is { } denied)
            return denied;

        await userService.CheckVerificationCodeAsync(request.UserId, request.Code, cancellationToken).ConfigureAwait(false);

        UserScope? refreshed = await userService.GetUserByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        return refreshed is null ? this.NotFoundProblem() : Ok(refreshed.ToUserResponse());
    }

    /// <summary>
    /// Checks if a verification email has been sent recently to enforce a rate limit cooldown.
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
    /// Registers the timestamp of a verification email send.
    /// </summary>
    private static void RegisterVerificationMailSend(int userId)
        => _lastVerificationMailByUserId[userId] = DateTimeOffset.UtcNow;
}
