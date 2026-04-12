using Hermes.Domain.Entities;
using Hermes.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

/// <summary>Preserves <c>POST api/v1/add/user</c> for existing clients; same behavior as <see cref="UsersController.Post"/>.</summary>
[ApiController]
[Route("api/v1")]
public class UserRegistrationCompatController(IUserService userService) : ControllerBase
{
    /// <remarks>
    /// <b>POST</b> <c>api/v1/add/user</c> — Body (same as <see cref="UsersController.Post"/>):
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
    [HttpPost("add/user")]
    public async Task<ActionResult<User>> PostAddUser([FromBody] User request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest("Name is required.");
        if (string.IsNullOrEmpty(request.PasswordHash))
            return BadRequest("Password is required.");

        await userService.RegisterUserAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(request);
    }
}
