using FluentResults;
using Hermes.Api.Http;
using Hermes.Application.Errors;
using Hermes.Domain.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Hermes.UnitTests.Api;

public sealed class ApiProblemResultPatternMatchingTests
{
    private sealed class TestController : ControllerBase
    {
    }

    private readonly TestController _controller = new();

    [Fact]
    public void ToProblemResult_DuplicateEmailError_Returns_Conflict409()
    {
        var error = new DuplicateEmailError("test@hermes.dev");
        ActionResult result = _controller.ToProblemResult(error);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Conflict", problem.Title);
        Assert.Contains("test@hermes.dev", problem.Detail);
    }

    [Fact]
    public void ToProblemResult_InvalidCurrentPasswordError_Returns_BadRequest400_With_CustomType()
    {
        var error = new InvalidCurrentPasswordError();
        ActionResult result = _controller.ToProblemResult(error);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(HermesProblemTypeConstants.WRONG_CURRENT_PASSWORD, problem.Type);
    }

    [Fact]
    public void ToProblemResult_UserNotFoundError_Returns_NotFound404()
    {
        var error = new UserNotFoundError(99);
        ActionResult result = _controller.ToProblemResult(error);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Not Found", problem.Title);
    }

    [Fact]
    public void ToProblemResult_InvalidCredentialsError_Returns_Unauthorized401()
    {
        var error = new InvalidCredentialsError();
        ActionResult result = _controller.ToProblemResult(error);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Unauthorized", problem.Title);
    }

    [Fact]
    public void ToProblemResult_TokenCompromisedError_Returns_Unauthorized401()
    {
        var error = new TokenCompromisedError("Token revoked");
        ActionResult result = _controller.ToProblemResult(error);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Unauthorized", problem.Title);
    }

    [Fact]
    public void ToProblemResult_GenericError_Returns_BadRequest400()
    {
        var error = new Error("Generic validation issue");
        ActionResult result = _controller.ToProblemResult(error);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Bad Request", problem.Title);
    }
}
