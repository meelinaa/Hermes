using FluentValidation;
using Hermes.Api.Http;
using Hermes.Api.Validation;
using Hermes.Application.Models;
using Hermes.Application.Security;
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
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        IValidator<LoginRequest> validator,
        IJwtTokenIssuer tokenIssuer,
        CancellationToken cancellationToken)
    {
        var fv = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!fv.IsValid)
            return fv.ToValidationProblem(this);

        var result = await userService.LoginAsync(request.NameOrEmail, request.Password, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            return this.UnauthorizedProblem(result.ErrorMessage);

        var access = tokenIssuer.Issue(result.UserId!.Value, result.Email, result.Name);
        return Ok(new
        {
            success = true,
            userId = result.UserId,
            accessToken = access.Token,
            tokenType = "Bearer",
            expiresAt = access.ExpiresAtUtc
        });
    }
}
