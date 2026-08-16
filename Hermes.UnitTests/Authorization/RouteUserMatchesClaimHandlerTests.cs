using System.Security.Claims;
using Hermes.Api.Authorization;
using Hermes.Api.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Authorization;

/// <summary>
/// Contains unit tests for <see cref="RouteUserMatchesClaimHandler"/>,
/// verifying policy evaluation against route parameters, JWT subject/user claims, and edge cases.
/// </summary>
public sealed class RouteUserMatchesClaimHandlerTests
{
    private static (AuthorizationHandlerContext context, RouteUserMatchesClaimPolicy requirement) CreateAuthContext(
        ClaimsPrincipal principal,
        string routeKey = "id")
    {
        RouteUserMatchesClaimPolicy requirement = new(routeKey);
        AuthorizationHandlerContext context = new([requirement], principal, null);
        return (context, requirement);
    }

    /// <summary>
    /// Tests that requirement evaluation does not succeed when <see cref="HttpContext"/> is null.
    /// </summary>
    [Fact]
    public async Task HandleRequirementAsync_Should_NotSucceed_WhenHttpContextIsNull()
    {
        // Arrange
        Mock<IHttpContextAccessor> accessor = new();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        RouteUserMatchesClaimHandler handler = new(accessor.Object);
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "42")]));
        var (context, requirement) = CreateAuthContext(principal);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// Tests that requirement evaluation does not succeed when the caller principal has no valid user ID claim.
    /// </summary>
    [Fact]
    public async Task HandleRequirementAsync_Should_NotSucceed_WhenUserHasNoUserIdClaim()
    {
        // Arrange
        DefaultHttpContext httpContext = new();
        httpContext.Request.RouteValues["id"] = "42";

        Mock<IHttpContextAccessor> accessor = new();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        RouteUserMatchesClaimHandler handler = new(accessor.Object);
        ClaimsPrincipal principal = new(new ClaimsIdentity()); // No claims
        var (context, requirement) = CreateAuthContext(principal);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// Tests that requirement evaluation does not succeed when the route does not contain the required key.
    /// </summary>
    [Fact]
    public async Task HandleRequirementAsync_Should_NotSucceed_WhenRouteKeyMissing()
    {
        // Arrange
        DefaultHttpContext httpContext = new(); // RouteValues empty
        Mock<IHttpContextAccessor> accessor = new();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        RouteUserMatchesClaimHandler handler = new(accessor.Object);
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "42")]));
        var (context, requirement) = CreateAuthContext(principal, routeKey: "id");

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// Tests that requirement evaluation does not succeed when the route value is non-integer or non-positive.
    /// </summary>
    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task HandleRequirementAsync_Should_NotSucceed_WhenRouteValueNonIntegerOrNonPositive(string invalidRouteValue)
    {
        // Arrange
        DefaultHttpContext httpContext = new();
        httpContext.Request.RouteValues["id"] = invalidRouteValue;

        Mock<IHttpContextAccessor> accessor = new();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        RouteUserMatchesClaimHandler handler = new(accessor.Object);
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "42")]));
        var (context, requirement) = CreateAuthContext(principal, routeKey: "id");

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// Tests that requirement evaluation does not succeed when the route user ID differs from the JWT user ID claim.
    /// </summary>
    [Fact]
    public async Task HandleRequirementAsync_Should_NotSucceed_WhenRouteIdDiffersFromClaimId()
    {
        // Arrange
        DefaultHttpContext httpContext = new();
        httpContext.Request.RouteValues["id"] = "99";

        Mock<IHttpContextAccessor> accessor = new();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        RouteUserMatchesClaimHandler handler = new(accessor.Object);
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "42")]));
        var (context, requirement) = CreateAuthContext(principal, routeKey: "id");

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// Tests that requirement evaluation succeeds when the route parameter matches the authenticated user ID claim.
    /// </summary>
    [Fact]
    public async Task HandleRequirementAsync_Should_Succeed_WhenRouteIdMatchesClaimId()
    {
        // Arrange
        DefaultHttpContext httpContext = new();
        httpContext.Request.RouteValues["userId"] = "42";

        Mock<IHttpContextAccessor> accessor = new();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        RouteUserMatchesClaimHandler handler = new(accessor.Object);
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim("sub", "42")]));
        var (context, requirement) = CreateAuthContext(principal, routeKey: "userId");

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }
}
