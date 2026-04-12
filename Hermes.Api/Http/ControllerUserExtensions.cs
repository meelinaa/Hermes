using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Http;

/// <summary>Reads the authenticated user id from the JWT (<see cref="ClaimTypes.NameIdentifier"/>) and enforces self-service access.</summary>
public static class ControllerUserExtensions
{
    /// <summary>Numeric user id from a validated access token (JWT).</summary>
    public static bool TryGetCurrentUserId(this ControllerBase controller, out int userId) =>
        controller.User.TryGetUserId(out userId);

    /// <summary>Same as <see cref="TryGetCurrentUserId"/> but for any <see cref="ClaimsPrincipal"/>.</summary>
    public static bool TryGetUserId(this ClaimsPrincipal principal, out int userId)
    {
        userId = 0;
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(id) && int.TryParse(id, out userId) && userId > 0;
    }

    /// <summary>
    /// Ensures <paramref name="resourceUserId"/> equals the caller's id from the token.
    /// Returns <c>null</c> when access is allowed; otherwise an <see cref="ActionResult"/> to return (401 or 403 ProblemDetails).
    /// </summary>
    public static ActionResult? WhenCannotAccessUser(this ControllerBase controller, int resourceUserId)
    {
        if (!controller.TryGetCurrentUserId(out var currentUserId))
            return controller.UnauthorizedProblem("Missing or invalid user identity in token.");

        if (currentUserId != resourceUserId)
            return controller.ForbiddenProblem("You can only access resources for your own account.");

        return null;
    }
}
