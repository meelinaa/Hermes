using FluentValidation.TestHelper;
using Hermes.Api.Validators.Newsletter;
using Hermes.Application.DTOs.NewsletterSubscription;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

/// <summary>
/// Unit tests for <see cref="UpdateNewsletterSubscriptionRequestValidator"/>.
/// </summary>
public sealed class UpdateNewsletterSubscriptionRequestValidatorTests
{
    private readonly UpdateNewsletterSubscriptionRequestValidator _validator = new();

    /// <summary>
    /// Verifies that the validator reports an error when the ID is non-positive.
    /// </summary>
    // [B]OUNDARY: Zero or negative subscription ID produces validation error
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ShouldInvalidate_WhenIdNotPositive(int invalidId)
    {
        // Arrange
        UpdateNewsletterSubscriptionRequestDto request = new() { Id = invalidId };

        // Act
        TestValidationResult<UpdateNewsletterSubscriptionRequestDto> result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Id);
    }

    /// <summary>
    /// Verifies that the validator passes when the ID is positive.
    /// </summary>
    // [R]IGHT: Positive subscription ID passes validation
    [Fact]
    public void ShouldValidate_WhenIdPositive()
    {
        // Arrange
        UpdateNewsletterSubscriptionRequestDto request = new() { Id = 1 };

        // Act
        TestValidationResult<UpdateNewsletterSubscriptionRequestDto> result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.Id);
    }
}
