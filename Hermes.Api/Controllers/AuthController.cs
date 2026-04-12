using Hermes.Application.Models;
using Hermes.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IUserService userService) : ControllerBase
{
    /// <summary>Login with display name or email plus plain password (BCrypt verify).</summary>
    /// <remarks>
    /// <b>POST</b> <c>api/v1/auth/login</c> — Body (application/json):
    /// <para>By email:</para>
    /// <code>
    /// {
    ///   "nameOrEmail": "max@example.com",
    ///   "password": "plain-password"
    /// }
    /// </code>
    /// <para>By display name:</para>
    /// <code>
    /// {
    ///   "nameOrEmail": "Max Mustermann",
    ///   "password": "plain-password"
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await userService.LoginAsync(request.NameOrEmail, request.Password, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            return Unauthorized(new { success = false, message = result.ErrorMessage });

        return Ok(new { success = true, userId = result.UserId });
    }
}
