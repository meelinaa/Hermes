using Hermes.Application.Mapping;
using Hermes.Domain.Enums;
using Xunit;

namespace Hermes.UnitTests.Mapping;

public sealed class IsoCodeMapperFrozenTests
{
    [Theory]
    [InlineData(Language.German, "de")]
    [InlineData(Language.English, "en")]
    public void LanguageIsoCodeMapper_ToIso639Code_Returns_CorrectCode(Language lang, string expected)
    {
        string code = LanguageIsoCodeMapper.ToIso639Code(lang);
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("de", Language.German)]
    [InlineData("DE", Language.German)]
    [InlineData("en", Language.English)]
    [InlineData("EN", Language.English)]
    public void LanguageIsoCodeMapper_TryGetLanguage_CaseInsensitive(string code, Language expected)
    {
        bool success = LanguageIsoCodeMapper.TryGetLanguage(code, out Language result);
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LanguageIsoCodeMapper_TryGetLanguage_NullOrEmpty_Returns_False()
    {
        Assert.False(LanguageIsoCodeMapper.TryGetLanguage(null, out _));
        Assert.False(LanguageIsoCodeMapper.TryGetLanguage("", out _));
        Assert.False(LanguageIsoCodeMapper.TryGetLanguage("   ", out _));
        Assert.False(LanguageIsoCodeMapper.TryGetLanguage("unknown", out _));
    }

    [Theory]
    [InlineData(Country.Germany, "de")]
    [InlineData(Country.USA, "us")]
    public void CountryIsoCodeMapper_ToIso3166Alpha2_Returns_CorrectCode(Country country, string expected)
    {
        string code = CountryIsoCodeMapper.ToIso3166Alpha2(country);
        Assert.Equal(expected, code);
    }

    [Theory]
    [InlineData("de", Country.Germany)]
    [InlineData("DE", Country.Germany)]
    [InlineData("us", Country.USA)]
    [InlineData("US", Country.USA)]
    public void CountryIsoCodeMapper_TryGetCountry_CaseInsensitive(string code, Country expected)
    {
        bool success = CountryIsoCodeMapper.TryGetCountry(code, out Country result);
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CountryIsoCodeMapper_TryGetCountry_InvalidLength_Returns_False()
    {
        Assert.False(CountryIsoCodeMapper.TryGetCountry(null, out _));
        Assert.False(CountryIsoCodeMapper.TryGetCountry("usa", out _));
        Assert.False(CountryIsoCodeMapper.TryGetCountry("d", out _));
    }

    [Theory]
    [InlineData(NewsCategory.Technology, "technology")]
    [InlineData(NewsCategory.Science, "science")]
    [InlineData(NewsCategory.Sports, "sports")]
    public void NewsCategoryMapper_ToApiString_Returns_LowercaseString(NewsCategory category, string expected)
    {
        string apiStr = NewsCategoryMapper.ToApiString(category);
        Assert.Equal(expected, apiStr);
    }

    [Theory]
    [InlineData("technology", NewsCategory.Technology)]
    [InlineData("TECHNOLOGY", NewsCategory.Technology)]
    [InlineData("science", NewsCategory.Science)]
    [InlineData("SPORTS", NewsCategory.Sports)]
    public void NewsCategoryMapper_TryGetCategory_CaseInsensitive(string input, NewsCategory expected)
    {
        bool success = NewsCategoryMapper.TryGetCategory(input, out NewsCategory result);
        Assert.True(success);
        Assert.Equal(expected, result);
    }
}
