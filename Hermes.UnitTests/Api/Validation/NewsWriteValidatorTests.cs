using FluentValidation.TestHelper;
using Hermes.Api.Validation;
using Hermes.Domain.Entities;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

/// <summary>
/// FluentValidation rules for news writes: owning user id must be a positive integer when validating API payloads.
/// </summary>
public sealed class NewsWriteValidatorTests
{
    private readonly NewsWriteValidator _validator = new();

    /// <summary>
    /// UserId &lt;= 0 must fail validation so invalid ownership cannot be persisted through the API layer.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ShouldInvalidate_WhenUserIdNotPositive(int invalidUserId)
    {
        // Arrange
        News entity = new News { UserId = invalidUserId };

        // Act
        TestValidationResult<News> result = _validator.TestValidate(entity);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    /// <summary>
    /// Positive UserId passes the ownership rule (other rules may apply elsewhere).
    /// </summary>
    [Fact]
    public void ShouldValidate_WhenUserIdPositive()
    {
        // Arrange
        News entity = new News { UserId = 1 };

        // Act
        TestValidationResult<News> result = _validator.TestValidate(entity);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.UserId);
    }
}
