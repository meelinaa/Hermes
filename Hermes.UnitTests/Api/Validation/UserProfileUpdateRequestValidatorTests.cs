using FluentValidation.TestHelper;
using Hermes.Api.Validation;
using Hermes.Application.DTOs.User;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

public sealed class UserProfileUpdateRequestValidatorTests
{
    private readonly UserProfileUpdateRequestValidator _sut = new();

    [Fact]
    public void Should_NotHaveError_When_RequestIsValid_WithoutPassword()
    {
        // Arrange
        UserProfileUpdateRequestDto request = new() { Id = 1, Name = "ValidName", Email = "test@example.com" };

        // Act
        TestValidationResult<UserProfileUpdateRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_NotHaveError_When_RequestIsValid_WithPassword()
    {
        // Arrange
        UserProfileUpdateRequestDto request = new() 
        { 
            Id = 1, 
            Name = "ValidName", 
            Email = "test@example.com",
            NewPassword = "NewPassword123",
            CurrentPassword = "OldPassword123"
        };

        // Act
        TestValidationResult<UserProfileUpdateRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_HaveError_When_IdIsInvalid(int invalidId)
    {
        // Arrange
        UserProfileUpdateRequestDto request = new() { Id = invalidId, Name = "ValidName", Email = "test@example.com" };

        // Act
        TestValidationResult<UserProfileUpdateRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage("User Id is required for update.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_HaveError_When_NameIsMissing(string? invalidName)
    {
        // Arrange
        UserProfileUpdateRequestDto request = new() { Id = 1, Name = invalidName!, Email = "test@example.com" };

        // Act
        TestValidationResult<UserProfileUpdateRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_HaveError_When_EmailIsMissing(string? invalidEmail)
    {
        // Arrange
        UserProfileUpdateRequestDto request = new() { Id = 1, Name = "ValidName", Email = invalidEmail! };

        // Act
        TestValidationResult<UserProfileUpdateRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required.");
    }

    [Fact]
    public void Should_HaveError_When_NewPasswordIsSet_ButCurrentPasswordIsMissing()
    {
        // Arrange
        UserProfileUpdateRequestDto request = new() 
        { 
            Id = 1, 
            Name = "ValidName", 
            Email = "test@example.com",
            NewPassword = "NewPassword123",
            CurrentPassword = null
        };

        // Act
        TestValidationResult<UserProfileUpdateRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CurrentPassword)
            .WithErrorMessage("Current password is required when setting a new password.");
    }

    [Fact]
    public void Should_NotHaveError_ForCurrentPassword_When_NewPasswordIsMissing()
    {
        // Arrange
        UserProfileUpdateRequestDto request = new() 
        { 
            Id = 1, 
            Name = "ValidName", 
            Email = "test@example.com",
            NewPassword = null,
            CurrentPassword = null // This is valid because NewPassword is not set
        };

        // Act
        TestValidationResult<UserProfileUpdateRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.CurrentPassword);
    }
}
