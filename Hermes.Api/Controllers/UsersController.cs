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

/// <summary>User CRUD under <c>api/v1/users</c>. JSON uses camelCase.</summary>
[Authorize]
[ApiController]
[Route("api/v1/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    private static readonly TimeSpan _verificationMailCooldown = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<int, DateTimeOffset> _lastVerificationMailByUserId = new();

    /// <summary>Register a new user with a plain password that is hashed before storage.</summary>
    /// <remarks>
    /// <b>POST</b> <c>api/v1/users</c> — Body (application/json):
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

    /// <summary>Update profile (name, e-mail, optional password change).</summary>
    /// <remarks>
    /// <b>PUT</b> <c>api/v1/users</c> — Body (camelCase):
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

    /// <summary>Delete user by id. No body.</summary>
    /// <remarks><b>DELETE</b> <c>api/v1/users/{id}</c></remarks>
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

    /// <summary>Get user by id. No body.</summary>
    /// <remarks><b>GET</b> <c>api/v1/users/{id}</c></remarks>
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

    /// <summary>Get user by e-mail address (path segment).</summary>
    /// <remarks><b>GET</b> <c>api/v1/users/by-email/{email}</c> — URL-encode the address (e.g. <c>%40</c> for <c>@</c>). Uses a fixed prefix so routes like <c>/api/v1/users/news</c> are not treated as an e-mail.</remarks>
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

    /// <summary>Sends a verification email to the authenticated user identified by <paramref name="id"/>.</summary>
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

    /// <summary>Submit e-mail verification code (six-digit). Returns updated profile when verified.</summary>
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

    /// <summary>
    /// Returns a cooldown error response when a verification e-mail was requested too recently; otherwise returns <c>null</c>.
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

    /// <summary>Stores the timestamp of the latest verification mail request for a user.</summary>
    private static void RegisterVerificationMailSend(int userId)
        => _lastVerificationMailByUserId[userId] = DateTimeOffset.UtcNow;
}
