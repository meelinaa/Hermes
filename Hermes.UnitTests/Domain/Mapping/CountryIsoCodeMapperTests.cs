using Hermes.Domain.Enums;
using Hermes.Domain.Mapping;
using Xunit;

namespace Hermes.UnitTests.Domain.Mapping;

/// <summary>
/// Specifications for ISO 3166 alpha-2 mapping between API strings and <see cref="Country"/> enum (NewsData filters).
/// </summary>
public sealed class CountryIsoCodeMapperTests
{
    /// <summary>
    /// Enum → API uses lowercase two-letter code from attributes.
    /// </summary>
    [Fact]
    public void ToIso3166Alpha2_ReturnsLowercaseAttributeCode()
    {
        Assert.Equal("de", CountryIsoCodeMapper.ToIso3166Alpha2(Country.Germany));
    }

    /// <summary>
    /// Parsing trims whitespace and accepts case-insensitive two-letter codes.
    /// </summary>
    [Fact]
    public void TryGetCountry_ReturnsTrue_ForAnyCaseTwoLetterCode()
    {
        Assert.True(CountryIsoCodeMapper.TryGetCountry("DE", out var c));
        Assert.Equal(Country.Germany, c);

        Assert.True(CountryIsoCodeMapper.TryGetCountry(" de ", out var c2));
        Assert.Equal(Country.Germany, c2);
    }

    /// <summary>
    /// Empty, wrong length, or unknown ISO codes fail TryGet without throwing.
    /// </summary>
    [Fact]
    public void TryGetCountry_ReturnsFalse_WhenInvalidLengthOrUnknown()
    {
        Assert.False(CountryIsoCodeMapper.TryGetCountry("", out _));
        Assert.False(CountryIsoCodeMapper.TryGetCountry("deu", out _));
        Assert.False(CountryIsoCodeMapper.TryGetCountry("zz", out _));
    }

    [Fact]
    public void ParseCountry_ReturnsEnum_WhenKnown()
    {
        Assert.Equal(Country.Germany, CountryIsoCodeMapper.ParseCountry("de"));
    }

    /// <summary>
    /// Parse throws <see cref="ArgumentException"/> with consistent parameter name for invalid codes.
    /// </summary>
    [Fact]
    public void ParseCountry_ThrowsArgumentException_WhenUnknown()
    {
        var ex = Assert.Throws<ArgumentException>(() => CountryIsoCodeMapper.ParseCountry("zz"));

        Assert.Equal("iso3166Alpha2", ex.ParamName);
    }
}
