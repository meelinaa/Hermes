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

    // [B]OUNDARY: Invalid email formats produce validation error
    [Theory]
    [InlineData("notanemail")]
    [InlineData("plainaddress")]
    [InlineData("@domain.com")]
    public void Should_HaveError_When_EmailIsInvalid(string invalidEmail)
    {
        // Arrange
        RegisterUserRequestDto request = new() { Name = "ValidName", Email = invalidEmail, Password = "ValidPassword" };

        // Act
        TestValidationResult<RegisterUserRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // [B]OUNDARY: Password shorter than 8 characters produces validation error
    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    public void Should_HaveError_When_PasswordIsTooShort(string shortPassword)
    {
        // Arrange
        RegisterUserRequestDto request = new() { Name = "ValidName", Email = "test@example.com", Password = shortPassword };

        // Act
        TestValidationResult<RegisterUserRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters.");
    }
}


