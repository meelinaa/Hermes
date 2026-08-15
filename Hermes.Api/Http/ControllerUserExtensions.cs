using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Http;

/// <summary>
/// Helper extension methods for retrieving authenticated user identity claims from controller execution contexts.
/// </summary>
public static class ControllerUserExtensions
{
    /// <summary>
    /// Attempts to extract the integer user ID of the currently authenticated principal from the controller user context.
    /// </summary>
    /// <param name="controller">The active controller instance.</param>
    /// <param name="userId">Out parameter set to the parsed user ID if successful, otherwise 0.</param>
    /// <returns>True if a valid user ID claim is present and positive, otherwise false.</returns>
    public static bool TryGetCurrentUserId(this ControllerBase controller, out int userId) =>
        controller.User.TryGetUserId(out userId);

    /// <summary>
    /// Attempts to extract and parse the numeric user ID claim (<see cref="ClaimTypes.NameIdentifier"/>) from a <see cref="ClaimsPrincipal"/>.
    /// </summary>
    /// <param name="principal">The claims principal representing the caller.</param>
    /// <param name="userId">Out parameter set to the parsed user ID if successful, otherwise 0.</param>
    /// <returns>True if a valid positive integer user ID claim is found, otherwise false.</returns>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out int userId)
    {
        userId = 0;
        string? id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return !string.IsNullOrEmpty(id) && int.TryParse(id, out userId) && userId > 0;
    }

    /// <summary>
    /// Enforces resource ownership checking by validating that the current user ID matches the target resource user ID.
    /// </summary>
    /// <param name="controller">The active controller instance.</param>
    /// <param name="resourceUserId">The user ID owning the target resource.</param>
    /// <returns>An HTTP 401/403 problem result if access is unauthorized/forbidden, or null if access is granted.</returns>
    public static ActionResult? WhenCannotAccessUser(this ControllerBase controller, int resourceUserId)
    {
        if (!controller.TryGetCurrentUserId(out int currentUserId))
            return controller.UnauthorizedProblem("Missing or invalid user identity in token.");

        if (currentUserId != resourceUserId)
            return controller.ForbiddenProblem("You can only access resources for your own account.");

        return null;
    }
}
