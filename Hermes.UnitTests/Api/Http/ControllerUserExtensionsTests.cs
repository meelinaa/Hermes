using System.Security.Claims;
using Hermes.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Hermes.UnitTests.Api.Http;

/// <summary>
/// Specifications for parsing authenticated user id from claims and enforcing resource ownership on controllers.
/// </summary>
public sealed class ControllerUserExtensionsTests
{
    private sealed class TestController : ControllerBase
    {
    }

    /// <summary>
    /// Builds a minimal controller with optional authenticated principal for extension-method tests.
    /// </summary>
    private static TestController CreateController(ClaimsPrincipal? user) =>
        new()
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user ?? new ClaimsPrincipal() } },
        };

    /// <summary>
    /// Unauthenticated principal (no identity) cannot yield a user id.
    /// </summary>
    [Fact]
    public void TryGetUserId_ShouldFail_WhenIdentityMissing()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity());

        Assert.False(principal.TryGetUserId(out int id));
        Assert.Equal(0, id);
    }

    /// <summary>
    /// NameIdentifier claim must parse as <see cref="int"/> — non-numeric values fail.
    /// </summary>
    [Fact]
    public void TryGetUserId_ShouldFail_WhenClaimNotInteger()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "abc")]));

        Assert.False(principal.TryGetUserId(out _));
    }

    /// <summary>
    /// Only strictly positive 32-bit integers are accepted (0, negative, int overflow rejected).
    /// </summary>
    [Fact]
    public void TryGetUserId_ShouldFail_WhenClaimZeroOrNegativeOrOverflow()
    {
        ClaimsPrincipal zero = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "0")]));
        Assert.False(zero.TryGetUserId(out _));

        ClaimsPrincipal negative = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "-3")]));
        Assert.False(negative.TryGetUserId(out _));

        ClaimsPrincipal overflow = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "2147483648")]));
        Assert.False(overflow.TryGetUserId(out _));
    }

    /// <summary>
    /// Valid positive integer in NameIdentifier resolves to user id.
    /// </summary>
    [Fact]
    public void TryGetUserId_Should_Succeed_WhenPositiveIntegerClaim()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "404")]));

        Assert.True(principal.TryGetUserId(out int id));
        Assert.Equal(404, id);
    }

    /// <summary>
    /// Controller helper reads the same claim from <see cref="ControllerBase.HttpContext"/> principal.
    /// </summary>
    [Fact]
    public void TryGetCurrentUserId_Should_Read_FromControllerPrincipal()
    {
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, "11")], authenticationType: "test");
        TestController c = CreateController(new ClaimsPrincipal(identity));

        Assert.True(c.TryGetCurrentUserId(out int uid));
        Assert.Equal(11, uid);
    }

    /// <summary>
    /// Resource matches caller user id — no authorization failure result.
    /// </summary>
    [Fact]
    public void WhenCannotAccessUser_Should_Allow_WhenResourceMatchesCaller()
    {
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, "5")], "test");
        TestController c = CreateController(new ClaimsPrincipal(identity));

        Assert.Null(c.WhenCannotAccessUser(5));
    }

    /// <summary>
    /// Without resolvable user id in principal, return 401 Unauthorized.
    /// </summary>
    [Fact]
    public void WhenCannotAccessUser_Should_Return401_WhenPrincipalMissingUserId()
    {
        TestController c = CreateController(new ClaimsPrincipal());

        ActionResult? result = c.WhenCannotAccessUser(5);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, obj.StatusCode);
    }

    /// <summary>
    /// Authenticated user id does not match resource owner — return 403 Forbidden.
    /// </summary>
    [Fact]
    public void WhenCannotAccessUser_Should_Return403_WhenResourceBelongsToAnotherUser()
    {
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, "5")], "test");
        TestController c = CreateController(new ClaimsPrincipal(identity));

        ActionResult? result = c.WhenCannotAccessUser(999);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }
}
