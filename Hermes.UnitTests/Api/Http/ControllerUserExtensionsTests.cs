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
        // [E]RROR: Unauthenticated claims identity returns false and zero user ID
        // Arrange
        ClaimsPrincipal principal = new(new ClaimsIdentity());

        // Act & Assert
        Assert.False(principal.TryGetUserId(out int id));
        Assert.Equal(0, id);
    }

    [Fact]
    public void TryGetUserId_ShouldFail_WhenClaimNotInteger()
    {
        // [B]OUNDARY: Non-numeric claim value cannot be parsed to an integer user ID
        // Arrange
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "abc")]));

        // Act & Assert
        Assert.False(principal.TryGetUserId(out _));
    }

    [Fact]
    public void TryGetUserId_ShouldFail_WhenClaimZeroOrNegativeOrOverflow()
    {
        // [B]OUNDARY: Boundary claim values (zero, negative, integer overflow) fail user ID parsing
        // Arrange
        ClaimsPrincipal zero = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "0")]));
        ClaimsPrincipal negative = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "-3")]));
        ClaimsPrincipal overflow = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "2147483648")]));

        // Act & Assert
        Assert.False(zero.TryGetUserId(out _));
        Assert.False(negative.TryGetUserId(out _));
        Assert.False(overflow.TryGetUserId(out _));
    }

    [Fact]
    public void TryGetUserId_Should_Succeed_WhenPositiveIntegerClaim()
    {
        // [R]IGHT: Valid positive integer claim parses user ID successfully
        // Arrange
        ClaimsPrincipal principal = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "404")]));

        // Act
        bool success = principal.TryGetUserId(out int id);

        // Assert
        Assert.True(success);
        Assert.Equal(404, id);
    }

    [Fact]
    public void TryGetCurrentUserId_Should_Read_FromControllerPrincipal()
    {
        // [R]IGHT: Reads authenticated user ID from controller HttpContext principal
        // Arrange
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, "11")], authenticationType: "test");
        TestController controller = CreateController(new ClaimsPrincipal(identity));

        // Act
        bool success = controller.TryGetCurrentUserId(out int uid);

        // Assert
        Assert.True(success);
        Assert.Equal(11, uid);
    }

    [Fact]
    public void WhenCannotAccessUser_Should_Allow_WhenResourceMatchesCaller()
    {
        // [R]IGHT: Access allowed when requested user ID matches authenticated caller
        // Arrange
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, "5")], "test");
        TestController controller = CreateController(new ClaimsPrincipal(identity));

        // Act
        ActionResult? result = controller.WhenCannotAccessUser(5);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void WhenCannotAccessUser_Should_Return401_WhenPrincipalMissingUserId()
    {
        // [E]RROR: Returns 401 Unauthorized when caller principal lacks valid user ID
        // Arrange
        TestController controller = CreateController(new ClaimsPrincipal());

        // Act
        ActionResult? result = controller.WhenCannotAccessUser(5);

        // Assert
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, obj.StatusCode);
    }

    [Fact]
    public void WhenCannotAccessUser_Should_Return403_WhenResourceBelongsToAnotherUser()
    {
        // [E]RROR: Returns 403 Forbidden when requesting resource belonging to another user
        // Arrange
        ClaimsIdentity identity = new([new Claim(ClaimTypes.NameIdentifier, "5")], "test");
        TestController controller = CreateController(new ClaimsPrincipal(identity));

        // Act
        ActionResult? result = controller.WhenCannotAccessUser(999);

        // Assert
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, obj.StatusCode);
    }
}
