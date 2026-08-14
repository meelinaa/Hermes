using Hermes.Domain.ValueObjects;
using Xunit;
using Hermes.Domain.Exceptions;
namespace Hermes.UnitTests.Domain.ValueObjects;

public sealed class EmailTests
{
    [Theory]
    [InlineData("test@example.com", "test@example.com")]
    [InlineData(" TEST@EXAMPLE.COM ", "test@example.com")]
    [InlineData("first.last+tag@sub.domain.org", "first.last+tag@sub.domain.org")]
    public void Parse_Should_ReturnEmail_WithTrimmedAndLowercasedValue(string input, string expected)
    {
        // Arrange is handled via InlineData

        // Act
        Email result = Email.Parse(input);

        // Assert
        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData(null)]
    public void Parse_Should_ThrowArgumentNullException_WhenInputIsNull(string? input)
    {
        // Arrange is handled via InlineData

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Email.Parse(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Should_ThrowArgumentException_WhenInputIsEmptyOrWhitespace(string input)
    {
        // Arrange is handled via InlineData

        // Act & Assert
        InvalidEmailException ex = Assert.Throws<InvalidEmailException>(() => Email.Parse(input));
        Assert.Contains("E-mail cannot be empty", ex.Message);
    }

    [Fact]
    public void Parse_Should_ThrowArgumentException_WhenInputExceedsMaxLength()
    {
        // Arrange
        string tooLongEmail = new string('a', 246) + "@test.com"; // Length is 255 (246 + 9)

        // Act & Assert
        InvalidEmailException ex = Assert.Throws<InvalidEmailException>(() => Email.Parse(tooLongEmail));
        Assert.Contains("E-mail is too long", ex.Message);
    }

    [Theory]
    [InlineData("test.example.com")] // Missing @
    [InlineData("@example.com")]     // Starts with @
    [InlineData("test@")]            // Ends with @
    [InlineData("test@@example.com")]// Multiple @
    [InlineData("a@b@c.com")]        // Multiple @
    public void Parse_Should_ThrowArgumentException_WhenFormatIsInvalid(string invalidEmail)
    {
        // Arrange is handled via InlineData

        // Act & Assert
        InvalidEmailException ex = Assert.Throws<InvalidEmailException>(() => Email.Parse(invalidEmail));
        Assert.Contains("Invalid e-mail format", ex.Message);
    }
}
