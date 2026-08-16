using FluentValidation.TestHelper;
using Hermes.Api.Validators.Auth;
using Hermes.Application.DTOs.Login;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

/// <summary>
/// Contains unit tests for <see cref="LogoutRequestValidator"/>.
/// </summary>
public sealed class LogoutRequestValidatorTests
{
    private readonly LogoutRequestValidator _sut = new();

    /// <summary>
    /// Tests that a valid refresh token payload passes validation.
    /// </summary>
    [Fact]
    public void Should_NotHaveError_When_RequestIsValid()
    {
        // Arrange
        LogoutRequestDto request = new() { RefreshToken = "valid-refresh-token-string" };

        // Act
        TestValidationResult<LogoutRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Tests that null or empty refresh token passes validation as it signifies revoking all tokens.
    /// </summary>
    [Fact]
    public void Should_NotHaveError_When_RefreshTokenIsNull()
    {
        // Arrange
        LogoutRequestDto request = new() { RefreshToken = null };

        // Act
        TestValidationResult<LogoutRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Tests that excessively long refresh tokens produce a validation error.
    /// </summary>
    [Fact]
    public void Should_HaveError_When_RefreshTokenExceedsMaximumLength()
    {
        // Arrange
        LogoutRequestDto request = new() { RefreshToken = new string('x', 1025) };

        // Act
        TestValidationResult<LogoutRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}
