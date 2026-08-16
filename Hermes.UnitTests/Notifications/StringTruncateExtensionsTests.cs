using Hermes.Notifications.Sending.Extensions;
using Xunit;

namespace Hermes.UnitTests.Notifications;

/// <summary>
/// Contains unit tests for <see cref="StringTruncateExtensions"/>,
/// verifying string truncation boundaries, suffix handling, and null/empty safety.
/// </summary>
public sealed class StringTruncateExtensionsTests
{
    /// <summary>
    /// Tests that <see cref="StringTruncateExtensions.Truncate"/> returns empty string
    /// when the input string is null or empty.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Truncate_Should_ReturnEmptyString_WhenInputNullOrEmpty(string? input)
    {
        // Act
        string result = input.Truncate(10);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// Tests that strings shorter than or equal to the maximum length are returned unmodified.
    /// </summary>
    [Theory]
    [InlineData("Hello", 10)]
    [InlineData("ExactLen", 8)]
    public void Truncate_Should_ReturnOriginalString_WhenLengthUnderOrEqualToLimit(string input, int maxLength)
    {
        // Act
        string result = input.Truncate(maxLength);

        // Assert
        Assert.Equal(input, result);
    }

    /// <summary>
    /// Tests that strings exceeding the maximum length are truncated and appended with the specified suffix.
    /// </summary>
    [Fact]
    public void Truncate_Should_TruncateAndAppendSuffix_WhenLengthExceedsLimit()
    {
        // Arrange
        string input = "This is a very long text that must be shortened.";

        // Act
        string result = input.Truncate(20, "...");

        // Assert
        Assert.Equal(20, result.Length);
        Assert.EndsWith("...", result);
        Assert.Equal("This is a very lo...", result);
    }

    /// <summary>
    /// Tests that custom suffixes (e.g. "[more]") are respected during length calculations.
    /// </summary>
    [Fact]
    public void Truncate_Should_RespectCustomSuffix()
    {
        // Arrange
        string input = "Antigravity Intelligence";

        // Act
        string result = input.Truncate(15, " [..]");

        // Assert
        Assert.Equal(15, result.Length);
        Assert.EndsWith(" [..]", result);
    }
}
