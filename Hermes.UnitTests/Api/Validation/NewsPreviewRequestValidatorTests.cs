using FluentValidation.TestHelper;
using Hermes.Api.Validators.Newsletter;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Domain.Enums;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

/// <summary>
/// Contains unit tests for <see cref="NewsPreviewRequestValidator"/>.
/// </summary>
public sealed class NewsPreviewRequestValidatorTests
{
    private readonly NewsPreviewRequestValidator _sut = new();

    /// <summary>
    /// Tests that valid preview search criteria pass validation.
    /// </summary>
    [Fact]
    public void Should_NotHaveError_When_RequestIsValid()
    {
        // Arrange
        NewsPreviewRequestDto request = new()
        {
            Keywords = "artificial intelligence",
            Categories = [NewsCategory.Technology],
            Languages = [Language.English],
            Countries = [Country.Germany]
        };

        // Act
        TestValidationResult<NewsPreviewRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Tests that excessively long keyword queries produce a validation error.
    /// </summary>
    [Fact]
    public void Should_HaveError_When_KeywordsExceedMaximumLength()
    {
        // Arrange
        NewsPreviewRequestDto request = new()
        {
            Keywords = new string('a', 201)
        };

        // Act
        TestValidationResult<NewsPreviewRequestDto> result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Keywords);
    }
}
