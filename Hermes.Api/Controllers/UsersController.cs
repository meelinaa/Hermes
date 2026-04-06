using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<User>> Post([FromBody] User request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest("Name is required.");

        await userService.RegisterUserAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(request);
    }

    [HttpPut]
    public async Task<ActionResult> Put([FromBody] User request, CancellationToken cancellationToken)
    {
        if (request.Id <= 0)
            return BadRequest("User Id is required for update.");
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest("Name is required.");

        await userService.UpdateUserAsync(request, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return NotFound();

        await userService.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<User>> GetById(int id, CancellationToken cancellationToken)
    {
        var user = await userService.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpGet("by-name")]
    public async Task<ActionResult<User>> GetByName([FromQuery] string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Query parameter 'name' is required.");

        var user = await userService.GetUserByNameAsync(name, cancellationToken).ConfigureAwait(false);
        return user is null ? NotFound() : Ok(user);
    }
}
