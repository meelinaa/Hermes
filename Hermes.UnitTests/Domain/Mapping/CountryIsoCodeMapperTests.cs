using Hermes.Application.Mapping;
using Hermes.Domain.Enums;
using Xunit;

namespace Hermes.UnitTests.Domain.Mapping;

/// <summary>
/// Contains unit tests for <see cref="CountryIsoCodeMapper"/>, verifying two-way ISO 3166-1 alpha-2 mapping,
/// case-insensitivity, whitespace tolerance, and error handling for unknown country codes.
/// </summary>
public sealed class CountryIsoCodeMapperTests
{
    /// <summary>
    /// Tests that <see cref="CountryIsoCodeMapper.ToIso3166Alpha2"/> correctly translates country enum members to lowercase two-letter ISO codes.
    /// </summary>
    /// <param name="country">The country enum value.</param>
    /// <param name="expectedCode">The expected ISO alpha-2 string.</param>
    [Theory]
    [InlineData(Country.Germany, "de")]
    [InlineData(Country.USA, "us")]
    [InlineData(Country.UnitedKingdom, "gb")]
    [InlineData(Country.France, "fr")]
    [InlineData(Country.Italy, "it")]
    [InlineData(Country.Spain, "es")]
    [InlineData(Country.Austria, "at")]
    [InlineData(Country.Switzerland, "ch")]
    [InlineData(Country.Netherlands, "nl")]
    [InlineData(Country.Poland, "pl")]
    public void ToIso3166Alpha2_Should_ReturnLowercaseIsoCode(Country country, string expectedCode)
    {
        // Act
        string code = CountryIsoCodeMapper.ToIso3166Alpha2(country);

        // Assert
        Assert.Equal(expectedCode, code);
    }

    /// <summary>
    /// Tests that <see cref="CountryIsoCodeMapper.TryGetCountry"/> successfully parses valid ISO codes regardless of casing or surrounding whitespace.
    /// </summary>
    /// <param name="inputCode">The raw code string.</param>
    /// <param name="expectedCountry">The expected parsed country enum member.</param>
    [Theory]
    [InlineData("de", Country.Germany)]
    [InlineData("DE", Country.Germany)]
    [InlineData(" de ", Country.Germany)]
    [InlineData("US", Country.USA)]
    [InlineData("gb", Country.UnitedKingdom)]
    [InlineData("FR", Country.France)]
    [InlineData("ch", Country.Switzerland)]
    public void TryGetCountry_Should_ReturnTrue_ForValidCodes(string inputCode, Country expectedCountry)
    {
        // Act
        bool success = CountryIsoCodeMapper.TryGetCountry(inputCode, out Country country);

        // Assert
        Assert.True(success);
        Assert.Equal(expectedCountry, country);
    }

    /// <summary>
    /// Tests that <see cref="CountryIsoCodeMapper.TryGetCountry"/> returns false for invalid, empty, or unknown codes.
    /// </summary>
    /// <param name="invalidCode">The invalid country code input.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("deu")]
    [InlineData("zz")]
    [InlineData("12")]
    public void TryGetCountry_Should_ReturnFalse_ForInvalidOrUnknownCodes(string? invalidCode)
    {
        // Act
        bool success = CountryIsoCodeMapper.TryGetCountry(invalidCode!, out Country country);

        // Assert
        Assert.False(success);
        Assert.Equal(default, country);
    }

    /// <summary>
    /// Tests that <see cref="CountryIsoCodeMapper.ParseCountry"/> resolves known codes and throws <see cref="ArgumentException"/> for unknown inputs.
    /// </summary>
    [Fact]
    public void ParseCountry_Should_ResolveKnown_AndThrowOnUnknown()
    {
        // Act & Assert
        Assert.Equal(Country.Germany, CountryIsoCodeMapper.ParseCountry("de"));
        Assert.Equal(Country.USA, CountryIsoCodeMapper.ParseCountry("US"));

        ArgumentException ex = Assert.Throws<ArgumentException>(() => CountryIsoCodeMapper.ParseCountry("zz"));
        Assert.Equal("iso3166Alpha2", ex.ParamName);
    }
}
