using Microsoft.AspNetCore.Authorization;

namespace Hermes.Api.Authorization;

/// <summary>
/// Authorization policy requirement specifying the route parameter key to match against the caller's user identity.
/// </summary>
public sealed class RouteUserMatchesClaimPolicy(string routeKey) : IAuthorizationRequirement
{
    /// <summary>
    /// The route parameter name (e.g. "userId" or "id") containing the target resource owner ID.
    /// </summary>
    public string RouteKey { get; } = routeKey ?? throw new ArgumentNullException(nameof(routeKey));
}
