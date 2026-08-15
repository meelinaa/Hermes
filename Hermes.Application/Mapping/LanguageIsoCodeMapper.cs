using System.Collections.Frozen;
using System.Reflection;
using Hermes.Domain.Enums;
using Hermes.Domain.Enums.Attributes;

namespace Hermes.Application.Mapping;

/// <summary>
/// High-performance bidirectional mapper between <see cref="Language"/> enum values and ISO 639-1 two-letter codes.
/// Uses immutable <see cref="FrozenDictionary{TKey, TValue}"/> for zero-allocation, O(1) hash lookups.
/// </summary>
public static class LanguageIsoCodeMapper
{
    private static readonly FrozenDictionary<Language, string> _toCode = BuildForward();
    private static readonly FrozenDictionary<string, Language> _fromCode = BuildReverse();

    /// <summary>
    /// Converts a <see cref="Language"/> enum value to its corresponding ISO 639-1 two-letter lowercase string code.
    /// </summary>
    /// <param name="language">The domain language enum value.</param>
    /// <returns>The ISO 639-1 code (e.g. "de", "en").</returns>
    public static string ToIso639Code(Language language)
    {
        if (!_toCode.TryGetValue(language, out string? code))
            throw new InvalidOperationException($"No ISO 639-1 code defined for {language}.");

        return code;
    }

    /// <summary>
    /// Attempts to parse an ISO 639-1 string code into a <see cref="Language"/> enum value without memory allocation.
    /// Case-insensitive matching via FrozenDictionary.
    /// </summary>
    /// <param name="iso639Code">The ISO 639-1 language code.</param>
    /// <param name="language">When this method returns, contains the matching <see cref="Language"/> enum value.</param>
    /// <returns>True if the language code was recognized; otherwise false.</returns>
    public static bool TryGetLanguage(string? iso639Code, out Language language)
    {
        language = default;
        if (string.IsNullOrWhiteSpace(iso639Code))
            return false;

        return _fromCode.TryGetValue(iso639Code.Trim(), out language);
    }

    /// <summary>
    /// Parses an ISO 639-1 string code into a <see cref="Language"/> enum value or throws an exception.
    /// </summary>
    /// <param name="iso639Code">The ISO 639-1 language code.</param>
    /// <returns>The matching <see cref="Language"/> enum value.</returns>
    public static Language ParseLanguage(string iso639Code)
    {
        if (TryGetLanguage(iso639Code, out Language language))
            return language;

        throw new ArgumentException($"Unknown ISO 639-1 code: {iso639Code}", nameof(iso639Code));
    }

    /// <summary>
    /// Builds the forward mapping from Language enum values to ISO 639-1 codes via reflection.
    /// </summary>
    /// <returns>A frozen dictionary of Language to ISO code.</returns>
    private static FrozenDictionary<Language, string> BuildForward()
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

        return map.ToFrozenDictionary();
    }

    /// <summary>
    /// Builds the reverse mapping from ISO 639-1 codes to Language enum values with case-insensitive comparisons.
    /// </summary>
    /// <returns>A frozen dictionary of ISO code to Language.</returns>
    private static FrozenDictionary<string, Language> BuildReverse()
    {
        Dictionary<string, Language> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<Language, string> kv in _toCode)
        {
            if (map.TryGetValue(kv.Value, out Language value))
            {
                throw new InvalidOperationException(
                    $"Duplicate ISO 639-1 code '{kv.Value}' for {kv.Key} and {value}.");
            }

            map[kv.Value] = kv.Key;
        }

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
