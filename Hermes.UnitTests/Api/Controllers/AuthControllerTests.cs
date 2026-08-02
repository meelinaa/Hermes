using System.Security.Claims;
using Hermes.Api.Controllers.Auth;
using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.Security;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Api.Controllers;

public sealed class AuthControllerTests
{
    private static AuthController CreateController(IUserAuthenticationService authService, ClaimsPrincipal? user = null)
    {
        AuthController controller = new(authService);
        if (user != null)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }
        else
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }
        return controller;
    }

    private static ClaimsPrincipal CreatePrincipalWithId(int userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "TestAuthType"));
    }

    [Fact]
    public async Task Login_Should_ReturnOkWithTokens_When_CredentialsAreValid()
    {
        // [R]IGHT: Valid user credentials return HTTP 200 OK with issued JWT tokens
        // Arrange
        Mock<IUserAuthenticationService> authServiceMock = new();
        authServiceMock.Setup(x => x.LoginAsync("valid@test.com", "Password123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResultDto(true, null, 1, "valid@test.com", "ValidUser"));

        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.IssueTokensAsync(1, "valid@test.com", "ValidUser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTokensResultDto("access-token", DateTime.UtcNow.AddMinutes(15), "refresh-token", DateTime.UtcNow.AddDays(7)));

        AuthController sut = CreateController(authServiceMock.Object);
        LoginRequestDto request = new() { NameOrEmail = "valid@test.com", Password = "Password123" };

        // Act
        IActionResult result = await sut.Login(request, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        OkObjectResult? okResult = result as OkObjectResult;
        Assert.NotNull(okResult);
        LoginResponseDto? response = okResult.Value as LoginResponseDto;
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("access-token", response.AccessToken);
    }

    [Fact]
    public async Task Login_Should_ReturnUnauthorizedProblem_When_LoginFails()
    {
        // [E]RROR: Invalid user credentials return HTTP 401 Unauthorized problem details
        // Arrange
        Mock<IUserAuthenticationService> authServiceMock = new();
        authServiceMock.Setup(x => x.LoginAsync("invalid@test.com", "WrongPassword", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResultDto(false, "Invalid credentials.", null));

        AuthController sut = CreateController(authServiceMock.Object);
        LoginRequestDto request = new() { NameOrEmail = "invalid@test.com", Password = "WrongPassword" };

        // Act
        IActionResult result = await sut.Login(request, Mock.Of<IAuthTokenService>(), CancellationToken.None);

        // Assert
        ObjectResult? objResult = result as ObjectResult;
        Assert.NotNull(objResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);
    }

    [Fact]
    public async Task Refresh_Should_ReturnOkWithNewTokens_When_RefreshTokenIsValid()
    {
        // [R]IGHT: Valid refresh token returns rotated JWT access/refresh token pair
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.RotateAsync("valid-refresh-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthTokensResultDto("new-access-token", DateTime.UtcNow.AddMinutes(15), "new-refresh-token", DateTime.UtcNow.AddDays(7)));

        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>());
        RefreshRequestDto request = new() { RefreshToken = "valid-refresh-token" };

        // Act
        IActionResult result = await sut.Refresh(request, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        OkObjectResult? okResult = result as OkObjectResult;
        Assert.NotNull(okResult);
        RefreshResponseDto? response = okResult.Value as RefreshResponseDto;
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("new-access-token", response.AccessToken);
    }

    [Fact]
    public async Task Refresh_Should_ReturnUnauthorizedProblem_When_RefreshTokenIsInvalid()
    {
        // [E]RROR: Invalid or expired refresh token returns HTTP 401 Unauthorized problem details
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.RotateAsync("invalid-refresh-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthTokensResultDto?)null);

        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>());
        RefreshRequestDto request = new() { RefreshToken = "invalid-refresh-token" };

        // Act
        IActionResult result = await sut.Refresh(request, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        ObjectResult? objResult = result as ObjectResult;
        Assert.NotNull(objResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);
    }

    [Fact]
    public async Task Logout_Should_ReturnUnauthorizedProblem_When_UserIdentityIsMissing()
    {
        // [E]RROR: Unauthenticated logout request returns HTTP 401 Unauthorized problem details
        // Arrange
        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>()); // No user claims

        // Act
        IActionResult result = await sut.Logout(new LogoutRequestDto() { RefreshToken = "refresh-token" }, Mock.Of<IAuthTokenService>(), CancellationToken.None);

        // Assert
        ObjectResult? objResult = result as ObjectResult;
        Assert.NotNull(objResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);
    }

    [Fact]
    public async Task Logout_Should_ReturnNoContent_When_SpecificTokenIsRevoked()
    {
        // [R]IGHT: Revoking a specific valid refresh token returns HTTP 204 No Content
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.TryRevokeRefreshForUserAsync("valid-refresh-token", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>(), CreatePrincipalWithId(1));

        // Act
        IActionResult result = await sut.Logout(new LogoutRequestDto() { RefreshToken = "valid-refresh-token" }, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Logout_Should_ReturnNoContent_When_AllTokensRevokedForUser()
    {
        // [B]OUNDARY: Null refresh token in logout revokes all active tokens for user
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.RevokeAllForUserAsync(1, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>(), CreatePrincipalWithId(1));

        // Act
        IActionResult result = await sut.Logout(new LogoutRequestDto() { RefreshToken = null }, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        authTokenServiceMock.Verify(x => x.RevokeAllForUserAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Logout_Should_ReturnUnauthorizedProblem_When_RevokeFails()
    {
        // [E]RROR: Revoking invalid refresh token returns HTTP 401 Unauthorized problem details
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.TryRevokeRefreshForUserAsync("invalid-refresh-token", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>(), CreatePrincipalWithId(1));

        // Act
        IActionResult result = await sut.Logout(new LogoutRequestDto() { RefreshToken = "invalid-refresh-token" }, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        ObjectResult? objResult = result as ObjectResult;
        Assert.NotNull(objResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);
    }
}
