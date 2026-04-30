using Hermes.Domain.Enums;
using Hermes.Domain.Mapping;
using Xunit;

namespace Hermes.UnitTests.Domain.Mapping;

/// <summary>
/// Specifications for ISO 639-1 mapping between API strings and <see cref="Language"/> enum (NewsData filters).
/// </summary>
public sealed class LanguageIsoCodeMapperTests
{
    /// <summary>
    /// Enum → API uses lowercase ISO code from attributes.
    /// </summary>
    [Fact]
    public void ToIso639Code_ReturnsLowercaseAttributeCode()
    {
        Assert.Equal("de", LanguageIsoCodeMapper.ToIso639Code(Language.German));
        Assert.Equal("en", LanguageIsoCodeMapper.ToIso639Code(Language.English));
    }

    /// <summary>
    /// Known codes resolve case-insensitively via TryGet.
    /// </summary>
    [Fact]
    public void TryGetLanguage_ReturnsTrue_ForNormalizedCode()
    {
        Assert.True(LanguageIsoCodeMapper.TryGetLanguage("EN", out var lang));
        Assert.Equal(Language.English, lang);
    }

    /// <summary>
    /// Empty or unknown codes fail without throwing.
    /// </summary>
    [Fact]
    public void TryGetLanguage_ReturnsFalse_WhenWhitespaceOrUnknown()
    {
        Assert.False(LanguageIsoCodeMapper.TryGetLanguage("", out _));
        Assert.False(LanguageIsoCodeMapper.TryGetLanguage("xxx", out _));
    }

    [Fact]
    public void ParseLanguage_ReturnsEnum_WhenKnown()
    {
        Assert.Equal(Language.English, LanguageIsoCodeMapper.ParseLanguage("en"));
    }

    /// <summary>
    /// Parse throws <see cref="ArgumentException"/> with consistent parameter name for unknown ISO codes.
    /// </summary>
    [Fact]
    public void ParseLanguage_ThrowsArgumentException_WhenUnknown()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => LanguageIsoCodeMapper.ParseLanguage("qq"));

        Assert.Equal("iso639Code", ex.ParamName);
    }
}
