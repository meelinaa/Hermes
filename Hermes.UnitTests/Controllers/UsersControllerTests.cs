using System.Security.Claims;
using FluentResults;
using Hermes.Api.Controllers.Users;
using Hermes.Application.DTOs.User;
using Hermes.Application.Errors;
using Hermes.Application.Ports.Inbound;
using Hermes.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Controllers;

/// <summary>
/// Contains unit tests for <see cref="UsersController"/>,
/// verifying registration, profile updates, account deletion, and email verification endpoints.
/// </summary>
public sealed class UsersControllerTests
{
    private static UsersController CreateController(
        IUserService? userService = null,
        IUserAuthenticationService? authService = null,
        IUserVerificationService? verificationService = null,
        int? authenticatedUserId = null)
    {
        var controller = new UsersController(
            userService ?? Mock.Of<IUserService>(),
            authService ?? Mock.Of<IUserAuthenticationService>(),
            verificationService ?? Mock.Of<IUserVerificationService>());

        DefaultHttpContext httpContext = new();
        if (authenticatedUserId.HasValue)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, authenticatedUserId.Value.ToString()),
                new Claim("sub", authenticatedUserId.Value.ToString())
            ], "TestAuth"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    /// <summary>
    /// Tests that <see cref="UsersController.SetNewUser"/> returns 201 Created when registration succeeds.
    /// </summary>
    [Fact]
    public async Task SetNewUser_Should_ReturnOk_WhenRegistrationSucceeds()
    {
        // Arrange
        Mock<IUserAuthenticationService> auth = new();
        auth.Setup(a => a.RegisterUserAsync(It.IsAny<RegisterUserRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new UserScopeDto { UserId = 10, Name = "Max", Email = "max@test.dev" }));

        UsersController sut = CreateController(authService: auth.Object);
        RegisterUserRequestDto request = new() { Name = "Max", Email = "max@test.dev", Password = "secret-password" };

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.SetNewUser(request, CancellationToken.None);

        // Assert
        CreatedAtActionResult created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        UserResponseDto response = Assert.IsType<UserResponseDto>(created.Value);
        Assert.Equal(10, response.UserId);
        Assert.Equal("Max", response.Name);
        Assert.Equal("max@test.dev", response.Email);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.SetNewUser"/> returns ProblemDetails when registration fails.
    /// </summary>
    [Fact]
    public async Task SetNewUser_Should_ReturnProblemDetails_WhenRegistrationFails()
    {
        // Arrange
        Mock<IUserAuthenticationService> auth = new();
        auth.Setup(a => a.RegisterUserAsync(It.IsAny<RegisterUserRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail(new DuplicateEmailError("max@test.dev")));

        UsersController sut = CreateController(authService: auth.Object);
        RegisterUserRequestDto request = new() { Name = "Max", Email = "max@test.dev", Password = "secret-password" };

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.SetNewUser(request, CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.UpdateUser"/> returns 403 Forbidden when caller ID does not match request ID.
    /// </summary>
    [Fact]
    public async Task UpdateUser_Should_ReturnForbidden_WhenCallerDoesNotMatchTargetId()
    {
        // Arrange (Caller is user 1, attempting to modify user 2)
        UsersController sut = CreateController(authenticatedUserId: 1);
        UserProfileUpdateRequestDto request = new() { Id = 2, Name = "NewName", Email = "new@test.dev" };

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.UpdateUser(2, request, CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.UpdateUser"/> returns ProblemDetails when update fails in service.
    /// </summary>
    [Fact]
    public async Task UpdateUser_Should_ReturnProblemDetails_WhenServiceUpdateFails()
    {
        // Arrange
        Mock<IUserAuthenticationService> auth = new();
        auth.Setup(a => a.UpdateUserAsync(1, "NewName", "new@test.dev", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail(new ValidationError("Invalid name.")));

        UsersController sut = CreateController(authService: auth.Object, authenticatedUserId: 1);
        UserProfileUpdateRequestDto request = new() { Id = 1, Name = "NewName", Email = "new@test.dev" };

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.UpdateUser(1, request, CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.UpdateUser"/> returns 200 Ok when caller owns the resource and update succeeds.
    /// </summary>
    [Fact]
    public async Task UpdateUser_Should_ReturnOk_WhenUpdateSucceeds()
    {
        // Arrange
        Mock<IUserAuthenticationService> auth = new();
        auth.Setup(a => a.UpdateUserAsync(1, "NewName", "new@test.dev", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock<IUserService> users = new();
        users.Setup(u => u.GetUserByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new UserScopeDto { UserId = 1, Name = "NewName", Email = "new@test.dev" }));

        UsersController sut = CreateController(userService: users.Object, authService: auth.Object, authenticatedUserId: 1);
        UserProfileUpdateRequestDto request = new() { Id = 1, Name = "NewName", Email = "new@test.dev" };

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.UpdateUser(1, request, CancellationToken.None);

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        UserResponseDto response = Assert.IsType<UserResponseDto>(ok.Value);
        Assert.Equal(1, response.UserId);
        Assert.Equal("NewName", response.Name);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.DeleteUser"/> returns 204 NoContent when deletion succeeds.
    /// </summary>
    [Fact]
    public async Task DeleteUser_Should_ReturnOk_WhenUserExistsAndDeleted()
    {
        // Arrange
        Mock<IUserService> users = new();
        UserScopeDto userScope = new() { UserId = 5, Name = "Alice", Email = "alice@test.dev" };
        users.Setup(u => u.GetUserByIdAsync(new UserId(5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(userScope));
        users.Setup(u => u.DeleteUserAsync(userScope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        UsersController sut = CreateController(userService: users.Object, authenticatedUserId: 5);

        // Act
        ActionResult actionResult = await sut.DeleteUser(5, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(actionResult);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.DeleteUser"/> returns 404 ProblemDetails when user is missing.
    /// </summary>
    [Fact]
    public async Task DeleteUser_Should_ReturnProblemDetails_WhenUserMissing()
    {
        // Arrange
        Mock<IUserService> users = new();
        users.Setup(u => u.GetUserByIdAsync(new UserId(99), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail(new UserNotFoundError(99)));

        UsersController sut = CreateController(userService: users.Object, authenticatedUserId: 99);

        // Act
        ActionResult actionResult = await sut.DeleteUser(99, CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.GetUserById"/> returns 404 NotFoundProblem when user does not exist.
    /// </summary>
    [Fact]
    public async Task GetUserById_Should_ReturnNotFound_WhenUserDoesNotExist()
    {
        // Arrange
        Mock<IUserService> users = new();
        users.Setup(u => u.GetUserByIdAsync(new UserId(404), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail(new UserNotFoundError(404)));

        UsersController sut = CreateController(userService: users.Object, authenticatedUserId: 404);

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.GetUserById(404, CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.GetUserById"/> returns 200 Ok when user exists.
    /// </summary>
    [Fact]
    public async Task GetUserById_Should_ReturnOk_WhenUserExists()
    {
        // Arrange
        Mock<IUserService> users = new();
        users.Setup(u => u.GetUserByIdAsync(new UserId(7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new UserScopeDto { UserId = 7, Name = "Agent", Email = "007@test.dev" }));

        UsersController sut = CreateController(userService: users.Object, authenticatedUserId: 7);

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.GetUserById(7, CancellationToken.None);

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        UserResponseDto response = Assert.IsType<UserResponseDto>(ok.Value);
        Assert.Equal(7, response.UserId);
        Assert.Equal("Agent", response.Name);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.GetUserByEmail"/> returns 400 BadRequestProblem when email is blank.
    /// </summary>
    [Fact]
    public async Task GetUserByEmail_Should_ReturnBadRequest_WhenEmailIsBlank()
    {
        // Arrange
        UsersController sut = CreateController(authenticatedUserId: 1);

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.GetUserByEmail("   ", CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.GetUserByEmail"/> returns 401 UnauthorizedProblem when no user is authenticated.
    /// </summary>
    [Fact]
    public async Task GetUserByEmail_Should_ReturnUnauthorized_WhenCallerUnauthenticated()
    {
        // Arrange
        UsersController sut = CreateController(authenticatedUserId: null);

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.GetUserByEmail("test@dev.io", CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.GetUserByEmail"/> returns 404 NotFoundProblem when user does not exist.
    /// </summary>
    [Fact]
    public async Task GetUserByEmail_Should_ReturnNotFound_WhenUserMissing()
    {
        // Arrange
        Mock<IUserService> users = new();
        users.Setup(u => u.GetUserByEmailAsync("missing@dev.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<UserScopeDto>("Missing"));

        UsersController sut = CreateController(userService: users.Object, authenticatedUserId: 1);

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.GetUserByEmail("missing@dev.io", CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.GetUserByEmail"/> enforces email path validation and caller identity scoping.
    /// </summary>
    [Fact]
    public async Task GetUserByEmail_Should_ReturnForbidden_WhenCallerDoesNotOwnEmail()
    {
        // Arrange
        Mock<IUserService> users = new();
        users.Setup(u => u.GetUserByEmailAsync("other@dev.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new UserScopeDto { UserId = 2, Email = "other@dev.io" }));

        UsersController sut = CreateController(userService: users.Object, authenticatedUserId: 1);

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.GetUserByEmail("other@dev.io", CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.GetUserByEmail"/> returns 200 Ok when caller owns the requested email.
    /// </summary>
    [Fact]
    public async Task GetUserByEmail_Should_ReturnOk_WhenCallerOwnsEmail()
    {
        // Arrange
        Mock<IUserService> users = new();
        users.Setup(u => u.GetUserByEmailAsync("owner@dev.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new UserScopeDto { UserId = 1, Email = "owner@dev.io", Name = "Owner" }));

        UsersController sut = CreateController(userService: users.Object, authenticatedUserId: 1);

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.GetUserByEmail("owner@dev.io", CancellationToken.None);

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        UserResponseDto response = Assert.IsType<UserResponseDto>(ok.Value);
        Assert.Equal(1, response.UserId);
        Assert.Equal("Owner", response.Name);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.SendVerificationMail"/> sends mail and returns 202 Accepted,
    /// and enforces cooldown on consecutive requests.
    /// </summary>
    [Fact]
    public async Task SendVerificationMail_Should_SendMail_AndEnforceCooldownOnSubsequentRequest()
    {
        // Arrange
        int uniqueUserId = 99123;
        Mock<IUserService> users = new();
        users.Setup(u => u.GetUserByIdAsync(new UserId(uniqueUserId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new UserScopeDto { UserId = uniqueUserId, Email = "cooldown@hermes.de", Name = "Cooldown" }));

        Mock<IUserVerificationService> verification = new();
        verification.Setup(v => v.SendVerificationMailAsync("cooldown@hermes.de", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        UsersController sut = CreateController(userService: users.Object, verificationService: verification.Object, authenticatedUserId: uniqueUserId);

        // Act 1 (First dispatch -> Success)
        ActionResult<SendVerificationMailResponseDto> actionResult1 = await sut.SendVerificationMail(uniqueUserId, CancellationToken.None);

        // Assert 1
        AcceptedResult accepted = Assert.IsType<AcceptedResult>(actionResult1.Result);
        SendVerificationMailResponseDto response = Assert.IsType<SendVerificationMailResponseDto>(accepted.Value);
        Assert.Equal(uniqueUserId, response.UserId);

        // Act 2 (Immediate second dispatch -> 400 with Retry-After header)
        ActionResult<SendVerificationMailResponseDto> actionResult2 = await sut.SendVerificationMail(uniqueUserId, CancellationToken.None);

        // Assert 2
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult2.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.False(string.IsNullOrEmpty(sut.Response.Headers.RetryAfter.ToString()));
    }

    /// <summary>
    /// Tests that <see cref="UsersController.CheckVerificationCode"/> returns 403 Forbidden when caller ID does not match.
    /// </summary>
    [Fact]
    public async Task CheckVerificationCode_Should_ReturnForbidden_WhenCallerDoesNotMatch()
    {
        // Arrange
        UsersController sut = CreateController(authenticatedUserId: 1);
        UserVerificationCodeRequestDto request = new() { UserId = 2, Code = 123456 };

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.CheckVerificationCode(2, request, CancellationToken.None);

        // Assert
        ObjectResult problem = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    /// <summary>
    /// Tests that <see cref="UsersController.CheckVerificationCode"/> verifies code and returns updated user on success.
    /// </summary>
    [Fact]
    public async Task CheckVerificationCode_Should_ReturnOk_WhenCodeValid()
    {
        // Arrange
        Mock<IUserVerificationService> verification = new();
        verification.Setup(v => v.CheckVerificationCodeAsync(new UserId(1), 123456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        Mock<IUserService> users = new();
        users.Setup(u => u.GetUserByIdAsync(new UserId(1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new UserScopeDto { UserId = 1, Name = "VerifiedUser", Email = "v@test.dev", IsEmailVerified = true }));

        UsersController sut = CreateController(userService: users.Object, verificationService: verification.Object, authenticatedUserId: 1);
        UserVerificationCodeRequestDto request = new() { UserId = 1, Code = 123456 };

        // Act
        ActionResult<UserResponseDto> actionResult = await sut.CheckVerificationCode(1, request, CancellationToken.None);

        // Assert
        OkObjectResult ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        UserResponseDto response = Assert.IsType<UserResponseDto>(ok.Value);
        Assert.Equal(1, response.UserId);
        Assert.True(response.IsEmailVerified);
    }
}
