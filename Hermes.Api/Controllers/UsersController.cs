using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

/// <summary>User CRUD under <c>api/v1/users</c>. JSON uses camelCase.</summary>
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
    [HttpPost]
    public async Task<ActionResult<UserScope>> SetNewUser([FromBody] User request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest("Name is required.");
        if (string.IsNullOrEmpty(request.PasswordHash))
            return BadRequest("Password is required.");

        UserScope userScope = await userService.RegisterUserAsync(request, cancellationToken).ConfigureAwait(false);

        return Ok(userScope);
    }

    /// <summary>Update an existing user.</summary>
    /// <remarks>
    /// <b>PUT</b> <c>api/v1/users</c> — Body:
    /// <code>
    /// {
    ///   "id": 1,
    ///   "name": "Max Mustermann",
    ///   "email": "max@example.com",
    ///   "passwordHash": "only-if-you-change-password-otherwise-omit-or-send-current-hash",
    ///   "isEmailVerified": true,
    ///   "twoFactorCode": null,
    ///   "twoFactorExpiry": null
    /// }
    /// </code>
    /// </remarks>
    [HttpPut]
    public async Task<ActionResult> UpdateUser([FromBody] User request, CancellationToken cancellationToken)
    {
        if (request.Id <= 0)
            return BadRequest("User Id is required for update.");
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest("Name is required.");

        await userService.UpdateUserAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>Delete user by id. No body.</summary>
    /// <remarks><b>DELETE</b> <c>api/v1/users/{id}</c></remarks>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteUser(int id, CancellationToken cancellationToken)
    {
        var user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return NotFound();

        await userService.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>Get user by id. No body.</summary>
    /// <remarks><b>GET</b> <c>api/v1/users/{id}</c></remarks>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<User>> GetUserById(int id, CancellationToken cancellationToken)
    {
        var user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>Get user by name. No body.</summary>
    /// <remarks><b>GET</b> <c>api/v1/users/by-name?name=Max%20Mustermann</c></remarks>
    [HttpGet("{email:string}")]
    public async Task<ActionResult<UserScope>> GetUserByEmail(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Query parameter 'email' is required.");

        UserScope user = await userService.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        return user is null ? NotFound() : Ok(user);
    }
}
