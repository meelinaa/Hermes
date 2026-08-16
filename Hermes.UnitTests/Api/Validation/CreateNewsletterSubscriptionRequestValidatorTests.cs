using FluentValidation.TestHelper;
using Hermes.Api.Validators.Newsletter;
using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Domain.Enums;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

/// <summary>
/// Contains unit tests for <see cref="CreateNewsletterSubscriptionRequestValidator"/>.
/// </summary>
public sealed class CreateNewsletterSubscriptionRequestValidatorTests
{
    private readonly CreateNewsletterSubscriptionRequestValidator _sut = new();

    /// <summary>
    /// Tests that a fully populated and valid subscription creation request passes validation without errors.
    /// </summary>
    [Fact]
    public void Should_NotHaveError_When_RequestIsValid()
    {
        // Arrange
        CreateNewsletterSubscriptionRequestDto request = new()
        {
            SendOnWeekdays = [Weekdays.Monday, Weekdays.Friday],
            SendAtTimes = [new TimeOnly(8, 30)],
            Keywords = ["technology", "ai"],
            Category = [NewsCategory.Technology],
            Languages = [Language.English],
            Countries = [Country.Germany],
            IsEnabled = true
        };

        // Act
        TestValidationResult<CreateNewsletterSubscriptionRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Tests that omitting weekdays produces a validation error.
    /// </summary>
    [Fact]
    public void Should_HaveError_When_SendOnWeekdaysIsEmpty()
    {
        // Arrange
        CreateNewsletterSubscriptionRequestDto request = new()
        {
            SendOnWeekdays = [],
            SendAtTimes = [new TimeOnly(8, 0)]
        };

        // Act
        TestValidationResult<CreateNewsletterSubscriptionRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SendOnWeekdays);
    }

    /// <summary>
    /// Tests that omitting delivery times produces a validation error.
    /// </summary>
    [Fact]
    public void Should_HaveError_When_SendAtTimesIsEmpty()
    {
        // Arrange
        CreateNewsletterSubscriptionRequestDto request = new()
        {
            SendOnWeekdays = [Weekdays.Monday],
            SendAtTimes = []
        };

        // Act
        TestValidationResult<CreateNewsletterSubscriptionRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SendAtTimes);
    }

    /// <summary>
    /// Tests that exceeding maximum keyword count produces a validation error.
    /// </summary>
    [Fact]
    public void Should_HaveError_When_KeywordsExceedMaximumCount()
    {
        // Arrange
        List<string> excessiveKeywords = Enumerable.Range(1, 25).Select(i => $"keyword{i}").ToList();
        CreateNewsletterSubscriptionRequestDto request = new()
        {
            SendOnWeekdays = [Weekdays.Monday],
            SendAtTimes = [new TimeOnly(8, 0)],
            Keywords = excessiveKeywords
        };

        // Act
        TestValidationResult<CreateNewsletterSubscriptionRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Keywords);
    }
}
