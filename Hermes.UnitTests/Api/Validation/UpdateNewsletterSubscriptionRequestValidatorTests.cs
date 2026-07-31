using FluentValidation.TestHelper;
using Hermes.Api.Validation;
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
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ShouldInvalidate_WhenIdNotPositive(int invalidId)
    {
        UpdateNewsletterSubscriptionRequest request = new() { Id = invalidId };

        TestValidationResult<UpdateNewsletterSubscriptionRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Id);
    }

    /// <summary>
    /// Verifies that the validator passes when the ID is positive.
    /// </summary>
    [Fact]
    public void ShouldValidate_WhenIdPositive()
    {
        UpdateNewsletterSubscriptionRequest request = new() { Id = 1 };

        TestValidationResult<UpdateNewsletterSubscriptionRequest> result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Id);
    }
}
