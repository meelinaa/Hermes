using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Xunit;

namespace Hermes.UnitTests.Domain.ValueObjects;

/// <summary>
/// Contains unit tests verifying domain value object parsing invariants, comparisons, and serialization for <see cref="UserId"/>, <see cref="NewsletterId"/>, and <see cref="Email"/>.
/// </summary>
public sealed class ValueObjectsInvariantTests
{
    /// <summary>
    /// Tests that <see cref="UserId.Parse(int)"/> rejects non-positive values (zero and negative).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-42)]
    public void UserId_Parse_Should_ThrowDomainValidationException_WhenNonPositive(int invalidValue)
    {
        // Act & Assert
        Assert.Throws<DomainValidationException>(() => UserId.Parse(invalidValue));
    }

    /// <summary>
    /// Tests that <see cref="UserId"/> comparisons and string formatting work correctly.
    /// </summary>
    [Fact]
    public void UserId_Should_SupportEqualityAndComparisons()
    {
        // Arrange
        UserId id1 = new(10);
        UserId id2 = new(20);
        UserId id1Copy = new(10);

        // Assert
        Assert.Equal(id1, id1Copy);
        Assert.True(id1 < id2);
        Assert.True(id1 <= id1Copy);
        Assert.True(id2 > id1);
        Assert.True(id2 >= id1);
        Assert.Equal("10", id1.ToString());
    }

    /// <summary>
    /// Tests that <see cref="NewsletterId.Parse(int)"/> rejects non-positive values (zero and negative).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-99)]
    public void NewsletterId_Parse_Should_ThrowDomainValidationException_WhenNonPositive(int invalidValue)
    {
        // Act & Assert
        Assert.Throws<DomainValidationException>(() => NewsletterId.Parse(invalidValue));
    }

    /// <summary>
    /// Tests that <see cref="NewsletterId"/> comparisons and string formatting work correctly.
    /// </summary>
    [Fact]
    public void NewsletterId_Should_SupportEqualityAndComparisons()
    {
        // Arrange
        NewsletterId id1 = new(5);
        NewsletterId id2 = new(15);
        NewsletterId id1Copy = new(5);

        // Assert
        Assert.Equal(id1, id1Copy);
        Assert.True(id1 < id2);
        Assert.True(id1 <= id1Copy);
        Assert.True(id2 > id1);
        Assert.True(id2 >= id1);
        Assert.Equal("5", id1.ToString());
    }

    /// <summary>
    /// Tests that <see cref="Email"/> in uninitialized default struct state returns empty string instead of null reference.
    /// </summary>
    [Fact]
    public void Email_DefaultStruct_Should_ReturnEmptyString_AndIndicateEmpty()
    {
        // Arrange
        Email defaultEmail = default;

        // Assert
        Assert.Equal(string.Empty, defaultEmail.Value);
        Assert.True(defaultEmail.IsEmpty);
        Assert.Equal(string.Empty, defaultEmail.ToString());
    }

    /// <summary>
    /// Tests that <see cref="Email.Parse(string?)"/> normalizes valid email addresses to lowercase and trimmed string.
    /// </summary>
    [Fact]
    public void Email_Parse_Should_NormalizeToLowercaseAndTrim()
    {
        // Arrange
        string raw = "  USER@Example.COM  ";

        // Act
        Email email = Email.Parse(raw);

        // Assert
        Assert.Equal("user@example.com", email.Value);
        Assert.False(email.IsEmpty);
        Assert.Equal("user@example.com", (string)email);
    }

    /// <summary>
    /// Tests that <see cref="Email.Parse(string?)"/> throws an <see cref="ArgumentNullException"/> when null.
    /// </summary>
    [Fact]
    public void Email_Parse_Should_ThrowArgumentNullException_WhenNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Email.Parse(null));
    }

    /// <summary>
    /// Tests that <see cref="Email.Parse(string?)"/> throws an <see cref="InvalidEmailException"/> for invalid email patterns.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@missing-user.com")]
    [InlineData("missing-domain@")]
    [InlineData("double@@domain.com")]
    public void Email_Parse_Should_ThrowInvalidEmailException_ForMalformedInputs(string invalidInput)
    {
        // Act & Assert
        Assert.Throws<InvalidEmailException>(() => Email.Parse(invalidInput));
    }
}
