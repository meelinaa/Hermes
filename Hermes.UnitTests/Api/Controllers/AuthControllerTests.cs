using System.Security.Claims;
using FluentResults;
using Hermes.Api.Controllers.Auth;
using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.Security;
using Hermes.Application.Errors;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services.Security;
using Hermes.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Api.Controllers;

/// <summary>
/// Contains unit tests for <see cref="AuthController"/>,
/// verifying user login, token issuance, token rotation, and single/all session logout revocation.
/// </summary>
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
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString())
        ], "TestAuthType"));
    }

    /// <summary>
    /// Tests that <see cref="AuthController.Login"/> returns HTTP 200 OK with issued JWT tokens when credentials are valid.
    /// </summary>
    [Fact]
    public async Task Login_Should_ReturnOkWithTokens_When_CredentialsAreValid()
    {
        // Arrange
        Mock<IUserAuthenticationService> authServiceMock = new();
        authServiceMock.Setup(x => x.LoginAsync("valid@test.com", "Password123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResultDto(true, null, 1, "valid@test.com", "ValidUser"));

        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.IssueTokensAsync(new UserId(1), "valid@test.com", "ValidUser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new AuthTokensResultDto("access-token", DateTime.UtcNow.AddMinutes(15), "refresh-token", DateTime.UtcNow.AddDays(7))));

        AuthController sut = CreateController(authServiceMock.Object);
        LoginRequestDto request = new() { NameOrEmail = Email.Parse("valid@test.com"), Password = "Password123" };

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

    /// <summary>
    /// Tests that <see cref="AuthController.Login"/> returns ProblemDetails when user authentication fails with a service error.
    /// </summary>
    [Fact]
    public async Task Login_Should_ReturnProblemDetails_When_AuthServiceFails()
    {
        // Arrange
        Mock<IUserAuthenticationService> authServiceMock = new();
        authServiceMock.Setup(x => x.LoginAsync("error@test.com", "Password123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<LoginResultDto>(new UserNotFoundError(404)));

        AuthController sut = CreateController(authServiceMock.Object);
        LoginRequestDto request = new() { NameOrEmail = Email.Parse("error@test.com"), Password = "Password123" };

        // Act
        IActionResult result = await sut.Login(request, Mock.Of<IAuthTokenService>(), CancellationToken.None);

        // Assert
        ObjectResult? objResult = result as ObjectResult;
        Assert.NotNull(objResult);
        Assert.Equal(StatusCodes.Status404NotFound, objResult.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="AuthController.Login"/> returns HTTP 401 Unauthorized ProblemDetails when credentials are invalid.
    /// </summary>
    [Fact]
    public async Task Login_Should_ReturnUnauthorizedProblem_When_LoginFails()
    {
        // Arrange
        Mock<IUserAuthenticationService> authServiceMock = new();
        authServiceMock.Setup(x => x.LoginAsync("invalid@test.com", "WrongPassword", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResultDto(false, "Invalid credentials.", null));

        AuthController sut = CreateController(authServiceMock.Object);
        LoginRequestDto request = new() { NameOrEmail = Email.Parse("invalid@test.com"), Password = "WrongPassword" };

        // Act
        IActionResult result = await sut.Login(request, Mock.Of<IAuthTokenService>(), CancellationToken.None);

        // Assert
        ObjectResult? objResult = result as ObjectResult;
        Assert.NotNull(objResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="AuthController.Login"/> returns ProblemDetails when token generation fails after successful login.
    /// </summary>
    [Fact]
    public async Task Login_Should_ReturnProblemDetails_When_TokenIssuanceFails()
    {
        // Arrange
        Mock<IUserAuthenticationService> authServiceMock = new();
        authServiceMock.Setup(x => x.LoginAsync("valid@test.com", "Password123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResultDto(true, null, 1, "valid@test.com", "ValidUser"));

        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.IssueTokensAsync(new UserId(1), "valid@test.com", "ValidUser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<AuthTokensResultDto>(new TokenCompromisedError("Token generation rejected.")));

        AuthController sut = CreateController(authServiceMock.Object);
        LoginRequestDto request = new() { NameOrEmail = Email.Parse("valid@test.com"), Password = "Password123" };

        // Act
        IActionResult result = await sut.Login(request, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        ObjectResult? objResult = result as ObjectResult;
        Assert.NotNull(objResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="AuthController.Refresh"/> returns rotated JWT access/refresh token pair on valid refresh token.
    /// </summary>
    [Fact]
    public async Task Refresh_Should_ReturnOkWithNewTokens_When_RefreshTokenIsValid()
    {
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.RotateAsync("valid-refresh-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new AuthTokensResultDto("new-access-token", DateTime.UtcNow.AddMinutes(15), "new-refresh-token", DateTime.UtcNow.AddDays(7))));

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

    /// <summary>
    /// Tests that <see cref="AuthController.Refresh"/> returns HTTP 401 Unauthorized problem details when refresh token is invalid.
    /// </summary>
    [Fact]
    public async Task Refresh_Should_ReturnUnauthorizedProblem_When_RefreshTokenIsInvalid()
    {
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.RotateAsync("invalid-refresh-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<AuthTokensResultDto>("error"));

        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>());
        RefreshRequestDto request = new() { RefreshToken = "invalid-refresh-token" };

        // Act
        IActionResult result = await sut.Refresh(request, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        ObjectResult? objResult = result as ObjectResult;
        Assert.NotNull(objResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="AuthController.Logout"/> returns HTTP 401 Unauthorized when caller identity claims are missing.
    /// </summary>
    [Fact]
    public async Task Logout_Should_ReturnUnauthorizedProblem_When_UserIdentityIsMissing()
    {
        // Arrange
        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>()); // No user claims

        // Act
        IActionResult result = await sut.Logout(new LogoutRequestDto() { RefreshToken = "refresh-token" }, Mock.Of<IAuthTokenService>(), CancellationToken.None);

        // Assert
        ObjectResult? objResult = result as ObjectResult;
        Assert.NotNull(objResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="AuthController.Logout"/> returns HTTP 204 No Content when revoking a specific valid refresh token.
    /// </summary>
    [Fact]
    public async Task Logout_Should_ReturnNoContent_When_SpecificTokenIsRevoked()
    {
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.TryRevokeRefreshForUserAsync("valid-refresh-token", new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>(), CreatePrincipalWithId(1));

        // Act
        IActionResult result = await sut.Logout(new LogoutRequestDto() { RefreshToken = "valid-refresh-token" }, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    /// <summary>
    /// Tests that <see cref="AuthController.Logout"/> revokes all active tokens when request body is null.
    /// </summary>
    [Fact]
    public async Task Logout_Should_ReturnNoContent_When_BodyIsNull()
    {
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.RevokeAllForUserAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>(), CreatePrincipalWithId(1));

        // Act
        IActionResult result = await sut.Logout(null, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        authTokenServiceMock.Verify(x => x.RevokeAllForUserAsync(new UserId(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="AuthController.Logout"/> revokes all active tokens for the user when RefreshToken is null or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Logout_Should_ReturnNoContent_When_AllTokensRevokedForUser(string? blankRefreshToken)
    {
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.RevokeAllForUserAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>(), CreatePrincipalWithId(1));

        // Act
        IActionResult result = await sut.Logout(new LogoutRequestDto() { RefreshToken = blankRefreshToken }, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
        authTokenServiceMock.Verify(x => x.RevokeAllForUserAsync(new UserId(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="AuthController.Logout"/> returns HTTP 401 Unauthorized problem details when token revocation fails.
    /// </summary>
    [Fact]
    public async Task Logout_Should_ReturnUnauthorizedProblem_When_RevokeFails()
    {
        // Arrange
        Mock<IAuthTokenService> authTokenServiceMock = new();
        authTokenServiceMock.Setup(x => x.TryRevokeRefreshForUserAsync("invalid-refresh-token", new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("error"));

        AuthController sut = CreateController(Mock.Of<IUserAuthenticationService>(), CreatePrincipalWithId(1));

        // Act
        IActionResult result = await sut.Logout(new LogoutRequestDto() { RefreshToken = "invalid-refresh-token" }, authTokenServiceMock.Object, CancellationToken.None);

        // Assert
        ObjectResult? objResult = result as ObjectResult;
        Assert.NotNull(objResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, objResult.StatusCode);
    }
}
