using FluentValidation;
using FluentValidation.Results;
using Hermes.Api.Filters;
using Hermes.Application.DTOs.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Filters;

/// <summary>
/// Contains unit tests for <see cref="AutoValidationFilter"/>,
/// verifying automatic discovery and invocation of FluentValidation validators and short-circuiting on validation errors.
/// </summary>
public sealed class AutoValidationFilterTests
{
    private static (ActionExecutingContext context, Mock<IServiceProvider> serviceProviderMock) CreateActionContext(
        Dictionary<string, object?> actionArguments)
    {
        Mock<IServiceProvider> serviceProviderMock = new();
        DefaultHttpContext httpContext = new()
        {
            RequestServices = serviceProviderMock.Object
        };

        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        ActionExecutingContext executingContext = new(
            actionContext,
            filters: [],
            actionArguments: actionArguments,
            controller: new object());

        return (executingContext, serviceProviderMock);
    }

    /// <summary>
    /// Tests that the action pipeline continues when no validator is registered for the action parameter.
    /// </summary>
    [Fact]
    public async Task OnActionExecutionAsync_Should_ContinuePipeline_WhenNoValidatorRegistered()
    {
        // Arrange
        RegisterUserRequestDto model = new() { Name = "Valid", Email = "v@test.dev", Password = "pw" };
        var (context, serviceProviderMock) = CreateActionContext(new Dictionary<string, object?>
        {
            ["model"] = model
        });

        serviceProviderMock.Setup(sp => sp.GetService(typeof(IValidator<RegisterUserRequestDto>))).Returns(null);

        AutoValidationFilter filter = new();
        bool nextInvoked = false;

        // Act
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextInvoked = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        // Assert
        Assert.True(nextInvoked);
        Assert.Null(context.Result);
        Assert.True(context.ModelState.IsValid);
    }

    /// <summary>
    /// Tests that the action pipeline continues when parameter validation passes.
    /// </summary>
    [Fact]
    public async Task OnActionExecutionAsync_Should_ContinuePipeline_WhenValidationPasses()
    {
        // Arrange
        RegisterUserRequestDto model = new() { Name = "Valid", Email = "v@test.dev", Password = "pw" };
        var (context, serviceProviderMock) = CreateActionContext(new Dictionary<string, object?>
        {
            ["model"] = model
        });

        Mock<IValidator<RegisterUserRequestDto>> validatorMock = new();
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        serviceProviderMock.Setup(sp => sp.GetService(typeof(IValidator<RegisterUserRequestDto>))).Returns(validatorMock.Object);

        AutoValidationFilter filter = new();
        bool nextInvoked = false;

        // Act
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextInvoked = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        // Assert
        Assert.True(nextInvoked);
        Assert.Null(context.Result);
        Assert.True(context.ModelState.IsValid);
    }

    /// <summary>
    /// Tests that the filter short-circuits the pipeline with a 400 BadRequestObjectResult containing validation errors.
    /// </summary>
    [Fact]
    public async Task OnActionExecutionAsync_Should_ShortCircuitWithBadRequest_WhenValidationFails()
    {
        // Arrange
        RegisterUserRequestDto model = new() { Name = "", Email = "v@test.dev", Password = "pw" };
        var (context, serviceProviderMock) = CreateActionContext(new Dictionary<string, object?>
        {
            ["model"] = model
        });

        Mock<IValidator<RegisterUserRequestDto>> validatorMock = new();
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([new ValidationFailure("Name", "Name is required")]));

        serviceProviderMock.Setup(sp => sp.GetService(typeof(IValidator<RegisterUserRequestDto>))).Returns(validatorMock.Object);

        AutoValidationFilter filter = new();
        bool nextInvoked = false;

        // Act
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextInvoked = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        // Assert
        Assert.False(nextInvoked);
        Assert.NotNull(context.Result);
        BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(context.Result);
        ValidationProblemDetails problemDetails = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
        Assert.True(problemDetails.Errors.ContainsKey("Name"));
    }
}
