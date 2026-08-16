using FluentValidation.TestHelper;
using Hermes.Api.Validators.NotificationLogs;
using Hermes.Application.DTOs.NotificationLogs;
using Hermes.Domain.Enums;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

/// <summary>
/// Contains unit tests for <see cref="CreateNotificationLogRequestValidator"/>.
/// </summary>
public sealed class CreateNotificationLogRequestValidatorTests
{
    private readonly CreateNotificationLogRequestValidator _sut = new();

    /// <summary>
    /// Tests that a valid notification log creation request passes validation.
    /// </summary>
    [Fact]
    public void Should_NotHaveError_When_RequestIsValid()
    {
        // Arrange
        CreateNotificationLogRequestDto request = new()
        {
            RetryCount = 0,
            Status = NotificationStatus.Sent,
            Channel = DeliveryChannel.Email,
            ErrorMessage = null,
            SentAt = DateTime.UtcNow
        };

        // Act
        TestValidationResult<CreateNotificationLogRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Tests that negative retry counts produce a validation error.
    /// </summary>
    [Fact]
    public void Should_HaveError_When_RetryCountIsNegative()
    {
        // Arrange
        CreateNotificationLogRequestDto request = new()
        {
            RetryCount = -1,
            Status = NotificationStatus.Pending,
            Channel = DeliveryChannel.Email
        };

        // Act
        TestValidationResult<CreateNotificationLogRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RetryCount);
    }
}
