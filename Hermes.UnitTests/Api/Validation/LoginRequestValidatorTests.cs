using FluentValidation.TestHelper;
using Hermes.Api.Validation;
using Hermes.Application.DTOs.Login;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _sut = new();

    [Fact]
    public void Should_NotHaveError_When_RequestIsValid()
    {
        // Arrange
        LoginRequestDto request = new() { NameOrEmail = "ValidUser", Password = "ValidPassword" };

        // Act
        TestValidationResult<LoginRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_HaveError_When_NameOrEmailIsMissing(string? invalidNameOrEmail)
    {
        // Arrange
        LoginRequestDto request = new() { NameOrEmail = invalidNameOrEmail!, Password = "ValidPassword" };

        // Act
        TestValidationResult<LoginRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NameOrEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_HaveError_When_PasswordIsMissing(string? invalidPassword)
    {
        // Arrange
        LoginRequestDto request = new() { NameOrEmail = "ValidUser", Password = invalidPassword! };

        // Act
        TestValidationResult<LoginRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
