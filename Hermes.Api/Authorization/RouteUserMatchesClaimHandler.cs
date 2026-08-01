using System.Globalization;
using Hermes.Api.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hermes.Api.Authorization;

public sealed class RouteUserMatchesClaimHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<RouteUserMatchesClaimPolicy>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RouteUserMatchesClaimPolicy requirement)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return Task.CompletedTask;

        if (!context.User.TryGetUserId(out int claimUserId))
            return Task.CompletedTask;

        RouteValueDictionary routeValues = httpContext.Request.RouteValues;
        if (!routeValues.TryGetValue(requirement.RouteKey, out object? routeValue))
            return Task.CompletedTask;

        if (!int.TryParse(routeValue?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int routeUserId) ||
            routeUserId <= 0)
            return Task.CompletedTask;

        if (routeUserId == claimUserId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
