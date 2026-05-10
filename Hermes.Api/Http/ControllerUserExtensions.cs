using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Http;

public static class ControllerUserExtensions
{
    public static bool TryGetCurrentUserId(this ControllerBase controller, out int userId) =>
        controller.User.TryGetUserId(out userId);

    public static bool TryGetUserId(this ClaimsPrincipal principal, out int userId)
    {
        userId = 0;
        string? id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(id) && int.TryParse(id, out userId) && userId > 0;
    }

    public static ActionResult? WhenCannotAccessUser(this ControllerBase controller, int resourceUserId)
    {
        if (!controller.TryGetCurrentUserId(out int currentUserId))
            return controller.UnauthorizedProblem("Missing or invalid user identity in token.");

        if (currentUserId != resourceUserId)
            return controller.ForbiddenProblem("You can only access resources for your own account.");

        return null;
    }
}
