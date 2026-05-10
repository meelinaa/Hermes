using Microsoft.AspNetCore.Authorization;

namespace Hermes.Api.Authorization;

/// <summary>Authorization requirement: named route value equals <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>.</summary>
public sealed class RouteUserMatchesClaimRequirement(string routeKey) : IAuthorizationRequirement
{
    /// <summary>Route-data key (<c>userId</c>, <c>id</c>, …).</summary>
    public string RouteKey { get; } = routeKey ?? throw new ArgumentNullException(nameof(routeKey));
}
