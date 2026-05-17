using Hermes.Api.Authorization;
using Hermes.Api.Http;
using Hermes.Api.Mapping;
using Hermes.Application.Models.User;
using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Collections.Concurrent;

namespace Hermes.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    private static readonly TimeSpan _verificationMailCooldown = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<int, DateTimeOffset> _lastVerificationMailByUserId = new();

    /// <remarks>
    /// <b>POST</b> <c>api/v1/users</c>:
    /// <code>
    /// {
    ///   "id": 0,
    ///   "name": "Max Mustermann",
    ///   "email": "max@example.com",
    ///   "password": "plain-password-here",
    ///   "isEmailVerified": false,
    ///   "twoFactorCode": null,
    ///   "twoFactorExpiry": null
    /// }
    /// </code>
    /// </remarks>
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<UserResponse>> SetNewUser([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Name))
            return this.BadRequestProblem("Name is required.");
        if (string.IsNullOrEmpty(request.Password))
            return this.BadRequestProblem("Password is required.");

        UserScope userScope = await userService.RegisterUserAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(userScope.ToUserResponse());
    }

    /// <remarks>
    /// <b>PUT</b> <c>api/v1/users</c>:
    /// <code>
    /// {
    ///   "id": 1,
    ///   "name": "Max Mustermann",
    ///   "email": "max@example.com",
    ///   "newPassword": "omit-or-empty-to-keep",
    ///   "currentPassword": "required-when-newPassword-is-set"
    /// }
    /// </code>
    /// </remarks>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPut]
    public async Task<ActionResult<UserResponse>> UpdateUser([FromBody] UserProfileUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.Id <= 0)
            return this.BadRequestProblem("User Id is required for update.");
        if (string.IsNullOrEmpty(request.Name))
            return this.BadRequestProblem("Name is required.");
        if (string.IsNullOrEmpty(request.Email))
            return this.BadRequestProblem("Email is required.");

        if (!string.IsNullOrWhiteSpace(request.NewPassword) && string.IsNullOrWhiteSpace(request.CurrentPassword))
            return this.BadRequestProblem("Current password is required when setting a new password.");

        if (this.WhenCannotAccessUser(request.Id) is { } denied)
            return denied;

        User user = new()
        {
            Id = request.Id,
            Name = request.Name,
            Email = request.Email,
            PasswordHash = request.NewPassword
        };

        try
        {
            await userService.UpdateUserAsync(user, request.CurrentPassword, cancellationToken).ConfigureAwait(false);
        }
        catch (WrongCurrentPasswordException wcp)
        {
            return this.WrongCurrentPasswordProblem(wcp.Message);
        }
        catch (ArgumentException ex)
        {
            return this.BadRequestProblem(ex.Message);
        }

        UserScope? updated;
        try
        {
            updated = await userService.GetUserByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (UserNotFoundException)
        {
            return this.NotFoundProblem();
        }

        return updated is null ? this.NotFoundProblem() : Ok(updated.ToUserResponse());
    }

    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        UserScope? user;
        try
        {
            user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (UserNotFoundException)
        {
            return this.NotFoundProblem();
        }

        if (user is null)
            return this.NotFoundProblem();

        await userService.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_ID)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserResponse>> GetUserById(int id, CancellationToken cancellationToken)
    {
        try
        {
            UserScope? user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return user is null ? this.NotFoundProblem() : Ok(user.ToUserResponse());
        }
        catch (UserNotFoundException)
        {
            return this.NotFoundProblem();
        }
    }

    /// <remarks>Prefix <c>by-email/</c> avoids collision with sibling routes (e.g. <c>/news</c>). Percent-encode <c>@</c>.</remarks>
    [HttpGet("by-email/{email}")]
    public async Task<ActionResult<UserResponse>> GetUserByEmail(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return this.BadRequestProblem("Path segment 'email' is required.");

        UserScope? user;
        try
        {
            user = await userService.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        }
        catch (UserNotFoundException)
        {
            return this.NotFoundProblem();
        }

        if (user is null)
            return this.NotFoundProblem();

        if (this.WhenCannotAccessUser(user.UserId) is { } denied)
            return denied;

        return Ok(user.ToUserResponse());
    }

    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_ID)]
    [EnableRateLimiting("VerifyMailPolicy")]
    [HttpPost("{id:int}/verify")]
    public async Task<ActionResult<SendVerificationMailResponse>> SendVerificationMail(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return this.BadRequestProblem("A valid user id is required.");

        UserScope? user;
        try
        {
            user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (UserNotFoundException)
        {
            return this.NotFoundProblem();
        }

        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return this.NotFoundProblem();

        if (TryGetVerificationMailCooldownResponse(id) is { } cooldownResult)
            return cooldownResult;

        await userService.SendVerificationMailAsync(user.Email, cancellationToken).ConfigureAwait(false);
        RegisterVerificationMailSend(id);
        return Ok(new SendVerificationMailResponse(id, user.Email));
    }

    [EnableRateLimiting("VerifyCodePolicy")]
    [HttpPost("verify/code")]
    public async Task<ActionResult<UserResponse>> CheckVerificationCode([FromBody] UserVerificationCodeRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return this.BadRequestProblem("Request body is required.");

        if (request.UserId <= 0)
            return this.BadRequestProblem("A valid user id is required.");

        if (request.Code is < 0 or > 999_999)
            return this.BadRequestProblem("Verification code must be between 0 and 999999.");

        if (this.WhenCannotAccessUser(request.UserId) is { } denied)
            return denied;

        await userService.CheckVerificationCodeAsync(request.UserId, request.Code, cancellationToken).ConfigureAwait(false);

        UserScope? refreshed;
        try
        {
            refreshed = await userService.GetUserByIdAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        }
        catch (UserNotFoundException)
        {
            return this.NotFoundProblem();
        }

        return refreshed is null ? this.NotFoundProblem() : Ok(refreshed.ToUserResponse());
    }

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

    private static void RegisterVerificationMailSend(int userId)
        => _lastVerificationMailByUserId[userId] = DateTimeOffset.UtcNow;
}
