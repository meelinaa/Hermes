using Hermes.Application.Mapping;
using Hermes.Domain.Enums;
using Xunit;

namespace Hermes.UnitTests.Domain.Mapping;

public sealed class LanguageIsoCodeMapperTests
{
    [Fact]
    public void ToIso639Code_ReturnsLowercaseAttributeCode()
    {
        Assert.Equal("de", LanguageIsoCodeMapper.ToIso639Code(Language.German));
        Assert.Equal("en", LanguageIsoCodeMapper.ToIso639Code(Language.English));
    }

    [Fact]
    public void TryGetLanguage_ReturnsTrue_ForNormalizedCode()
    {
        Assert.True(LanguageIsoCodeMapper.TryGetLanguage("EN", out Language language));
        Assert.Equal(Language.English, language);
    }

    [Fact]
    public void TryGetLanguage_ReturnsFalse_WhenWhitespaceOrUnknown()
    {
        Assert.False(LanguageIsoCodeMapper.TryGetLanguage("", out _));
        Assert.False(LanguageIsoCodeMapper.TryGetLanguage("xxx", out _));
    }

    [Fact]
    public void ParseLanguage_ReturnsEnum_WhenKnown() => Assert.Equal(Language.English, LanguageIsoCodeMapper.ParseLanguage("en"));

    [Fact]
    public void ParseLanguage_ThrowsArgumentException_WhenUnknown()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => LanguageIsoCodeMapper.ParseLanguage("qq"));

        Assert.Equal("iso639Code", ex.ParamName);
    }
}
