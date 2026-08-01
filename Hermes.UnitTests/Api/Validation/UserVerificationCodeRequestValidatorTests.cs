using FluentValidation.TestHelper;
using Hermes.Api.Validation;
using Hermes.Application.DTOs.User;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

public sealed class UserVerificationCodeRequestValidatorTests
{
    private readonly UserVerificationCodeRequestValidator _sut = new();

    [Fact]
    public void Should_NotHaveError_When_RequestIsValid()
    {
        // Arrange
        UserVerificationCodeRequestDto request = new() { UserId = 1, Code = 123456 };

        // Act
        TestValidationResult<UserVerificationCodeRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_HaveError_When_UserIdIsInvalid(int invalidUserId)
    {
        // Arrange
        UserVerificationCodeRequestDto request = new() { UserId = invalidUserId, Code = 123456 };

        // Act
        TestValidationResult<UserVerificationCodeRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("A valid user id is required.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000)]
    public void Should_HaveError_When_CodeIsOutOfRange(int invalidCode)
    {
        // Arrange
        UserVerificationCodeRequestDto request = new() { UserId = 1, Code = invalidCode };

        // Act
        TestValidationResult<UserVerificationCodeRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Code)
            .WithErrorMessage("Verification code must be between 0 and 999999.");
    }
}
