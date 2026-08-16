using Hermes.Application.Mapping;
using Hermes.Domain.Enums;
using Xunit;

namespace Hermes.UnitTests.Domain.Mapping;

/// <summary>
/// Contains unit tests for <see cref="LanguageIsoCodeMapper"/>, verifying ISO 639-1 language code translation,
/// case-insensitivity, whitespace trimming, and argument error validation.
/// </summary>
public sealed class LanguageIsoCodeMapperTests
{
    /// <summary>
    /// Tests that <see cref="LanguageIsoCodeMapper.ToIso639Code"/> converts language enum members into lowercase two-letter ISO strings.
    /// </summary>
    /// <param name="language">The language enum value.</param>
    /// <param name="expectedCode">The expected ISO 639-1 code string.</param>
    [Theory]
    [InlineData(Language.German, "de")]
    [InlineData(Language.English, "en")]
    [InlineData(Language.French, "fr")]
    [InlineData(Language.Spanish, "es")]
    [InlineData(Language.Italian, "it")]
    [InlineData(Language.Portuguese, "pt")]
    [InlineData(Language.Dutch, "nl")]
    [InlineData(Language.Polish, "pl")]
    public void ToIso639Code_Should_ReturnLowercaseIsoCode(Language language, string expectedCode)
    {
        // Act
        string code = LanguageIsoCodeMapper.ToIso639Code(language);

        // Assert
        Assert.Equal(expectedCode, code);
    }

    /// <summary>
    /// Tests that <see cref="LanguageIsoCodeMapper.TryGetLanguage"/> parses valid ISO codes regardless of uppercase/lowercase or surrounding spaces.
    /// </summary>
    /// <param name="inputCode">The raw code string.</param>
    /// <param name="expectedLanguage">The expected parsed language enum member.</param>
    [Theory]
    [InlineData("de", Language.German)]
    [InlineData("EN", Language.English)]
    [InlineData(" fr ", Language.French)]
    [InlineData("ES", Language.Spanish)]
    [InlineData("it", Language.Italian)]
    [InlineData("pl", Language.Polish)]
    public void TryGetLanguage_Should_ReturnTrue_ForValidCodes(string inputCode, Language expectedLanguage)
    {
        // Act
        bool success = LanguageIsoCodeMapper.TryGetLanguage(inputCode, out Language language);

        // Assert
        Assert.True(success);
        Assert.Equal(expectedLanguage, language);
    }

    /// <summary>
    /// Tests that <see cref="LanguageIsoCodeMapper.TryGetLanguage"/> returns false when presented with invalid, blank, or unknown language strings.
    /// </summary>
    /// <param name="invalidCode">The invalid language code.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("xxx")]
    [InlineData("deu")]
    [InlineData("99")]
    public void TryGetLanguage_Should_ReturnFalse_ForInvalidOrUnknownCodes(string? invalidCode)
    {
        // Act
        bool success = LanguageIsoCodeMapper.TryGetLanguage(invalidCode!, out Language language);

        // Assert
        Assert.False(success);
        Assert.Equal(default, language);
    }

    /// <summary>
    /// Tests that <see cref="LanguageIsoCodeMapper.ParseLanguage"/> resolves known codes and throws <see cref="ArgumentException"/> for unknown inputs.
    /// </summary>
    [Fact]
    public void ParseLanguage_Should_ResolveKnown_AndThrowOnUnknown()
    {
        // Act & Assert
        Assert.Equal(Language.German, LanguageIsoCodeMapper.ParseLanguage("de"));
        Assert.Equal(Language.English, LanguageIsoCodeMapper.ParseLanguage("EN"));

        ArgumentException ex = Assert.Throws<ArgumentException>(() => LanguageIsoCodeMapper.ParseLanguage("qq"));
        Assert.Equal("iso639Code", ex.ParamName);
    }
}
