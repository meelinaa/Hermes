using System.Security.Claims;
using Hermes.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Hermes.UnitTests.Api.Http;

public sealed class ControllerUserExtensionsTests
{
    private sealed class TestController : ControllerBase
    {
    }

    private static TestController CreateController(ClaimsPrincipal? user) =>
        new()
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user ?? new ClaimsPrincipal() } },
        };

    [Fact]
    public void TryGetUserId_ShouldFail_WhenIdentityMissing()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity());

        Assert.False(principal.TryGetUserId(out int id));
        Assert.Equal(0, id);
    }

    [Fact]
    public void TryGetUserId_ShouldFail_WhenClaimNotInteger()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "abc")]));

        Assert.False(principal.TryGetUserId(out _));
    }

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

    [Fact]
    public void TryGetUserId_Should_Succeed_WhenPositiveIntegerClaim()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "404")]));

        Assert.True(principal.TryGetUserId(out int id));
        Assert.Equal(404, id);
    }

    [Fact]
    public void TryGetCurrentUserId_Should_Read_FromControllerPrincipal()
    {
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, "11")], authenticationType: "test");
        TestController c = CreateController(new ClaimsPrincipal(identity));

        Assert.True(c.TryGetCurrentUserId(out int uid));
        Assert.Equal(11, uid);
    }

    [Fact]
    public void WhenCannotAccessUser_Should_Allow_WhenResourceMatchesCaller()
    {
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, "5")], "test");
        TestController c = CreateController(new ClaimsPrincipal(identity));

        Assert.Null(c.WhenCannotAccessUser(5));
    }

    [Fact]
    public void WhenCannotAccessUser_Should_Return401_WhenPrincipalMissingUserId()
    {
        TestController c = CreateController(new ClaimsPrincipal());

        ActionResult? result = c.WhenCannotAccessUser(5);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, obj.StatusCode);
    }

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
