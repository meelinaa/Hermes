using System.Reflection;
using Hermes.Domain.Enums;

namespace Hermes.Domain.Mapping;

/// <summary>
/// Maps <see cref="Language"/> to ISO 639-1 codes and back using <see cref="LanguageIsoCodeAttribute"/>.
/// </summary>
public static class LanguageIsoCodeMapper
{
    private static readonly IReadOnlyDictionary<Language, string> _toCode = BuildForward();
    private static readonly IReadOnlyDictionary<string, Language> _fromCode = BuildReverse();

    /// <summary>
    /// Returns the ISO 639-1 code (lowercase) for the given language.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the enum member has no attribute.</exception>
    public static string ToIso639Code(Language language)
    {
        if (!_toCode.TryGetValue(language, out string? code))
            throw new InvalidOperationException($"No ISO 639-1 code defined for {language}.");
        
        return code;
    }

    /// <summary>
    /// Resolves a language from an ISO 639-1 code (comparison is case-insensitive).
    /// </summary>
    public static bool TryGetLanguage(string iso639Code, out Language language)
    {
        language = default;
        if (string.IsNullOrWhiteSpace(iso639Code))
            return false;

        return _fromCode.TryGetValue(iso639Code.Trim().ToLowerInvariant(), out language);
    }

    /// <summary>
    /// Resolves a language from an ISO 639-1 code or throws if unknown.
    /// </summary>
    public static Language ParseLanguage(string iso639Code)
    {
        if (TryGetLanguage(iso639Code, out Language language))
            return language;
        
        throw new ArgumentException($"Unknown ISO 639-1 code: {iso639Code}", nameof(iso639Code));
    }

    /// <summary>Builds the mapping from <see cref="Language"/> enum values to ISO codes.</summary>
    private static Dictionary<Language, string> BuildForward()
    {
        Dictionary<Language, string> map = [];
        foreach (Language value in Enum.GetValues<Language>())
        {
            FieldInfo? field = typeof(Language).GetField(value.ToString());
            LanguageIsoCodeAttribute? attr = field?.GetCustomAttribute<LanguageIsoCodeAttribute>();
            if (attr is null)
                throw new InvalidOperationException($"Language.{value} is missing [{nameof(LanguageIsoCodeAttribute)}].");
            
            map[value] = attr.Code;
        }

        return map;
    }

    /// <summary>Builds the reverse mapping from ISO codes to <see cref="Language"/> values.</summary>
    private static Dictionary<string, Language> BuildReverse()
    {
        Dictionary<string, Language> map = new(StringComparer.Ordinal);
        foreach (KeyValuePair<Language, string> kv in _toCode)
        {
            if (map.TryGetValue(kv.Value, out Language value))
            {
                throw new InvalidOperationException(
                    $"Duplicate ISO 639-1 code '{kv.Value}' for {kv.Key} and {value}.");
            }

            map[kv.Value] = kv.Key;
        }

        return map;
    }
}
