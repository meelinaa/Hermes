using Hermes.Application.Mapping;
using Hermes.Domain.Enums;
using Xunit;

namespace Hermes.UnitTests.Domain.Mapping;

public sealed class CountryIsoCodeMapperTests
{
    [Fact]
    public void ToIso3166Alpha2_ReturnsLowercaseAttributeCode() => Assert.Equal("de", CountryIsoCodeMapper.ToIso3166Alpha2(Country.Germany));

    [Fact]
    public void TryGetCountry_ReturnsTrue_ForAnyCaseTwoLetterCode()
    {
        Assert.True(CountryIsoCodeMapper.TryGetCountry("DE", out Country country));
        Assert.Equal(Country.Germany, country);

        Assert.True(CountryIsoCodeMapper.TryGetCountry(" de ", out Country country2));
        Assert.Equal(Country.Germany, country2);
    }

    [Fact]
    public void TryGetCountry_ReturnsFalse_WhenInvalidLengthOrUnknown()
    {
        Assert.False(CountryIsoCodeMapper.TryGetCountry("", out _));
        Assert.False(CountryIsoCodeMapper.TryGetCountry("deu", out _));
        Assert.False(CountryIsoCodeMapper.TryGetCountry("zz", out _));
    }

    [Fact]
    public void ParseCountry_ReturnsEnum_WhenKnown() => Assert.Equal(Country.Germany, CountryIsoCodeMapper.ParseCountry("de"));

    [Fact]
    public void ParseCountry_ThrowsArgumentException_WhenUnknown()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => CountryIsoCodeMapper.ParseCountry("zz"));

        Assert.Equal("iso3166Alpha2", ex.ParamName);
    }
}
