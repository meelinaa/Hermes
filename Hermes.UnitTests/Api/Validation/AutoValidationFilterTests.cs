using FluentValidation;
using FluentValidation.Results;
using Hermes.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

public sealed class AutoValidationFilterTests
{
    public sealed class DummyDto { }

    private static ActionExecutingContext CreateContext(object? argument, IServiceProvider serviceProvider)
    {
        DefaultHttpContext httpContext = new() { RequestServices = serviceProvider };
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ActionExecutingContext context = new(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
        
        if (argument != null)
        {
            context.ActionArguments["dummy"] = argument;
        }

        return context;
    }

    [Fact]
    public async Task OnActionExecutionAsync_Should_CallNext_When_ArgumentIsNull()
    {
        // Arrange
        AutoValidationFilter sut = new();
        Mock<IServiceProvider> serviceProviderMock = new();
        ActionExecutingContext context = CreateContext(null, serviceProviderMock.Object);
        bool nextCalled = false;
        ActionExecutionDelegate next = () => 
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        };

        // Act
        await sut.OnActionExecutionAsync(context, next);

        // Assert
        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Should_CallNext_When_ValidatorIsNotFound()
    {
        // Arrange
        AutoValidationFilter sut = new();
        Mock<IServiceProvider> serviceProviderMock = new();
        serviceProviderMock.Setup(x => x.GetService(typeof(IValidator<DummyDto>))).Returns((object?)null);
        
        ActionExecutingContext context = CreateContext(new DummyDto(), serviceProviderMock.Object);
        bool nextCalled = false;
        ActionExecutionDelegate next = () => 
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        };

        // Act
        await sut.OnActionExecutionAsync(context, next);

        // Assert
        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Should_CallNext_When_ValidationSucceeds()
    {
        // Arrange
        AutoValidationFilter sut = new();
        DummyDto dummyDto = new();
        
        Mock<IValidator<DummyDto>> validatorMock = new();
        validatorMock.Setup(x => x.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult()); // valid result

        Mock<IServiceProvider> serviceProviderMock = new();
        serviceProviderMock.Setup(x => x.GetService(typeof(IValidator<DummyDto>)))
            .Returns(validatorMock.Object);
        
        ActionExecutingContext context = CreateContext(dummyDto, serviceProviderMock.Object);
        bool nextCalled = false;
        ActionExecutionDelegate next = () => 
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        };

        // Act
        await sut.OnActionExecutionAsync(context, next);

        // Assert
        Assert.True(nextCalled);
        Assert.Null(context.Result);
        Assert.True(context.ModelState.IsValid);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Should_ShortCircuitWithBadRequest_When_ValidationFails()
    {
        // Arrange
        AutoValidationFilter sut = new();
        DummyDto dummyDto = new();
        
        ValidationResult validationResult = new(new[] 
        {
            new ValidationFailure("Property", "Error message")
        });

        Mock<IValidator<DummyDto>> validatorMock = new();
        validatorMock.Setup(x => x.ValidateAsync(It.IsAny<IValidationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        Mock<IServiceProvider> serviceProviderMock = new();
        serviceProviderMock.Setup(x => x.GetService(typeof(IValidator<DummyDto>)))
            .Returns(validatorMock.Object);
        
        ActionExecutingContext context = CreateContext(dummyDto, serviceProviderMock.Object);
        bool nextCalled = false;
        ActionExecutionDelegate next = () => 
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), new object()));
        };

        // Act
        await sut.OnActionExecutionAsync(context, next);

        // Assert
        Assert.False(nextCalled, "next delegate should not be called when validation fails");
        Assert.False(context.ModelState.IsValid);
        
        BadRequestObjectResult? badRequestResult = context.Result as BadRequestObjectResult;
        Assert.NotNull(badRequestResult);
        
        ValidationProblemDetails? problemDetails = badRequestResult.Value as ValidationProblemDetails;
        Assert.NotNull(problemDetails);
        Assert.True(problemDetails.Errors.ContainsKey("Property"));
        Assert.Equal("Error message", problemDetails.Errors["Property"][0]);
    }
}
