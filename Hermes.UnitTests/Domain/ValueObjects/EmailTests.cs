using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Xunit;

namespace Hermes.UnitTests.Domain.ValueObjects;

/// <summary>
/// Contains unit tests for the <see cref="Email"/> value object,
/// verifying RFC-conformant string parsing, normalization, length boundaries, and implicit conversions.
/// </summary>
public sealed class EmailTests
{
    /// <summary>
    /// Tests that <see cref="Email.Parse"/> trims whitespace and lowercases the email address.
    /// </summary>
    [Theory]
    [InlineData("test@example.com", "test@example.com")]
    [InlineData(" TEST@EXAMPLE.COM ", "test@example.com")]
    [InlineData("first.last+tag@sub.domain.org", "first.last+tag@sub.domain.org")]
    public void Parse_Should_ReturnEmail_WithTrimmedAndLowercasedValue(string input, string expected)
    {
        // Act
        Email result = Email.Parse(input);

        // Assert
        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, result.ToString());
    }

    /// <summary>
    /// Tests that <see cref="Email.Parse"/> throws an <see cref="ArgumentNullException"/> when input is null.
    /// </summary>
    [Fact]
    public void Parse_Should_ThrowArgumentNullException_WhenInputIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Email.Parse(null));
    }

    /// <summary>
    /// Tests that <see cref="Email.Parse"/> throws an <see cref="InvalidEmailException"/> when input is empty or whitespace.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Should_ThrowInvalidEmailException_WhenInputIsEmptyOrWhitespace(string input)
    {
        // Act & Assert
        InvalidEmailException ex = Assert.Throws<InvalidEmailException>(() => Email.Parse(input));
        Assert.Contains("E-mail cannot be empty", ex.Message);
    }

    /// <summary>
    /// Tests that an email with exactly 254 characters (the RFC maximum) parses successfully.
    /// </summary>
    [Fact]
    public void Parse_Should_Succeed_WhenInputIsExactlyMaxLength254()
    {
        // Arrange (245 chars + "@test.com" (9 chars) = 254 chars)
        string maxValidEmail = new string('a', 245) + "@test.com";

        // Act
        Email result = Email.Parse(maxValidEmail);

        // Assert
        Assert.Equal(254, result.Value.Length);
    }

    /// <summary>
    /// Tests that <see cref="Email.Parse"/> throws an <see cref="InvalidEmailException"/> when input exceeds 254 characters.
    /// </summary>
    [Fact]
    public void Parse_Should_ThrowInvalidEmailException_WhenInputExceedsMaxLength()
    {
        // Arrange
        string tooLongEmail = new string('a', 246) + "@test.com"; // Length is 255 (246 + 9)

        // Act & Assert
        InvalidEmailException ex = Assert.Throws<InvalidEmailException>(() => Email.Parse(tooLongEmail));
        Assert.Contains("E-mail is too long", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="Email.Parse"/> throws an <see cref="InvalidEmailException"/> when the format is invalid.
    /// </summary>
    [Theory]
    [InlineData("test.example.com")]  // Missing @
    [InlineData("@example.com")]      // Starts with @
    [InlineData("test@")]             // Ends with @
    [InlineData("test@@example.com")] // Multiple @
    [InlineData("a@b@c.com")]         // Multiple @
    public void Parse_Should_ThrowInvalidEmailException_WhenFormatIsInvalid(string invalidEmail)
    {
        // Act & Assert
        InvalidEmailException ex = Assert.Throws<InvalidEmailException>(() => Email.Parse(invalidEmail));
        Assert.Contains("Invalid e-mail format", ex.Message);
    }

    /// <summary>
    /// Tests implicit conversion operators between <see cref="Email"/> and <see cref="string"/>.
    /// </summary>
    [Fact]
    public void ImplicitConversions_Should_ConvertBetweenEmailAndString()
    {
        // Act
        Email email = "user@hermes.de";
        string emailString = email;

        // Assert
        Assert.Equal("user@hermes.de", email.Value);
        Assert.Equal("user@hermes.de", emailString);
    }
}
