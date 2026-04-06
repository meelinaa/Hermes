using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

/// <summary>Preserves <c>POST api/v1/add/user</c> for existing clients; same behavior as <see cref="UsersController.Post"/>.</summary>
[ApiController]
[Route("api/v1")]
public class UserRegistrationCompatController(IUserService userService) : ControllerBase
{
    [HttpPost("add/user")]
    public async Task<ActionResult<User>> PostAddUser([FromBody] User request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest("Name is required.");

        await userService.RegisterUserAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(request);
    }
}
