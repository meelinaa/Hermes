using FluentValidation.TestHelper;
using Hermes.Api.Validators.Auth;
using Hermes.Application.DTOs.User;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

public sealed class RegisterUserRequestValidatorTests
{
    private readonly RegisterUserRequestValidator _sut = new();

    // [R]IGHT: Valid user registration request passes validation
    [Fact]
    public void Should_NotHaveError_When_RequestIsValid()
    {
        // Arrange
        RegisterUserRequestDto request = new() { Name = "ValidName", Email = "test@example.com", Password = "ValidPassword" };

        // Act
        TestValidationResult<RegisterUserRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    // [B]OUNDARY: Null, empty, or whitespace Name produces validation error
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_HaveError_When_NameIsMissing(string? invalidName)
    {
        // Arrange
        RegisterUserRequestDto request = new() { Name = invalidName!, Email = "test@example.com", Password = "ValidPassword" };

        // Act
        TestValidationResult<RegisterUserRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");
    }

    // [B]OUNDARY: Null, empty, or whitespace Password produces validation error
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_HaveError_When_PasswordIsMissing(string? invalidPassword)
    {
        // Arrange
        RegisterUserRequestDto request = new() { Name = "ValidName", Email = "test@example.com", Password = invalidPassword! };

        // Act
        TestValidationResult<RegisterUserRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required.");
    }
}


