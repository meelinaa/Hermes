using Hermes.Api.Http;
using Hermes.Application.Models;
using Hermes.Application.Models.User;
using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

/// <summary>User CRUD under <c>api/v1/users</c>. JSON uses camelCase.</summary>
[Authorize]
[ApiController]
[Route("api/v1/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>Register a new user. Plain password is sent in <c>passwordHash</c>; it is hashed before storage.</summary>
    /// <remarks>
    /// <b>POST</b> <c>api/v1/users</c> — Body (application/json):
    /// <code>
    /// {
    ///   "id": 0,
    ///   "name": "Max Mustermann",
    ///   "email": "max@example.com",
    ///   "passwordHash": "plain-password-here",
    ///   "isEmailVerified": false,
    ///   "twoFactorCode": null,
    ///   "twoFactorExpiry": null
    /// }
    /// </code>
    /// </remarks>
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<UserScope>> SetNewUser([FromBody] User request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Name))
            return this.BadRequestProblem("Name is required.");
        if (string.IsNullOrEmpty(request.PasswordHash))
            return this.BadRequestProblem("Password is required.");

        UserScope userScope = await userService.RegisterUserAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(userScope);
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
    [HttpPut]
    public async Task<ActionResult> UpdateUser([FromBody] UserProfileUpdateRequest request, CancellationToken cancellationToken)
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

        var user = new User
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
        catch (ArgumentException ex)
        {
            return this.BadRequestProblem(ex.Message);
        }

        return Ok();
    }

    /// <summary>Delete user by id. No body.</summary>
    /// <remarks><b>DELETE</b> <c>api/v1/users/{id}</c></remarks>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(id) is { } denied)
            return denied;

        var user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return this.NotFoundProblem();

        await userService.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>Get user by id. No body.</summary>
    /// <remarks><b>GET</b> <c>api/v1/users/{id}</c></remarks>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserScope>> GetUserById(int id, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(id) is { } denied)
            return denied;

        var user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return user is null ? this.NotFoundProblem() : Ok(user);
    }

    /// <summary>Get user by e-mail address (path segment).</summary>
    /// <remarks><b>GET</b> <c>api/v1/users/by-email/{email}</c> — URL-encode the address (e.g. <c>%40</c> for <c>@</c>). Uses a fixed prefix so routes like <c>/api/v1/users/news</c> are not treated as an e-mail.</remarks>
    [HttpGet("by-email/{email}")]
    public async Task<ActionResult<UserScope>> GetUserByEmail(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return this.BadRequestProblem("Path segment 'email' is required.");

        UserScope? user = await userService.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return this.NotFoundProblem();

        if (this.WhenCannotAccessUser(user.UserId) is { } denied)
            return denied;

        return Ok(user);
    }

    [HttpGet("verify/{email}")]
    public async Task<ActionResult> SendVerificationMail(string email, CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(email) || email.Length == 0)
            return this.BadRequestProblem("Path segment 'email' is required.");

        await userService.SendVerificationMailAsync(email, cancellationToken).ConfigureAwait(false);

        return Ok(email);
    }

    /// <summary>Submit e-mail verification code (six-digit). Returns 200 when the account is marked verified.</summary>
    [HttpPost("verify/code")]
    public async Task<ActionResult> CheckVerificationCode([FromBody] UserVerificationCodeRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return this.BadRequestProblem("Request body is required.");

        if (request.UserId <= 0)
            return this.BadRequestProblem("A valid user id is required.");

        if (request.Code < 0 || request.Code > 999_999)
            return this.BadRequestProblem("Verification code must be between 0 and 999999.");

        if (this.WhenCannotAccessUser(request.UserId) is { } denied)
            return denied;

        await userService.CheckVerificationCodeAsync(request.UserId, request.Code, cancellationToken).ConfigureAwait(false);
        return Ok();
    }
}
