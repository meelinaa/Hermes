using Microsoft.AspNetCore.Authorization;

namespace Hermes.Api.Authorization;

public sealed class RouteUserMatchesClaimRequirement(string routeKey) : IAuthorizationRequirement
{
    public string RouteKey { get; } = routeKey ?? throw new ArgumentNullException(nameof(routeKey));
}
