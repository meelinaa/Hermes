using FluentValidation.TestHelper;
using Hermes.Api.Validation;
using Hermes.Application.Models.News;
using Xunit;

namespace Hermes.UnitTests.Api.Validation;

public sealed class UpdateNewsRequestValidatorTests
{
    private readonly UpdateNewsRequestValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ShouldInvalidate_WhenIdNotPositive(int invalidId)
    {
        UpdateNewsRequest request = new() { Id = invalidId };

        TestValidationResult<UpdateNewsRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.Id);
    }

    [Fact]
    public void ShouldValidate_WhenIdPositive()
    {
        UpdateNewsRequest request = new() { Id = 1 };

        TestValidationResult<UpdateNewsRequest> result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(r => r.Id);
    }
}
