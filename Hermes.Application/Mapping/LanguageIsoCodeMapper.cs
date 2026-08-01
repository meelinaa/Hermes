using System.Reflection;
using Hermes.Domain.Enums;

namespace Hermes.Application.Mapping;

public static class LanguageIsoCodeMapper
{
    private static readonly IReadOnlyDictionary<Language, string> _toCode = BuildForward();
    private static readonly IReadOnlyDictionary<string, Language> _fromCode = BuildReverse();

    public static string ToIso639Code(Language language)
    {
        if (!_toCode.TryGetValue(language, out string? code))
            throw new InvalidOperationException($"No ISO 639-1 code defined for {language}.");

        return code;
    }

    public static bool TryGetLanguage(string iso639Code, out Language language)
    {
        language = default;
        if (string.IsNullOrWhiteSpace(iso639Code))
            return false;

        return _fromCode.TryGetValue(iso639Code.Trim().ToLowerInvariant(), out language);
    }

    public static Language ParseLanguage(string iso639Code)
    {
        if (TryGetLanguage(iso639Code, out Language language))
            return language;

        throw new ArgumentException($"Unknown ISO 639-1 code: {iso639Code}", nameof(iso639Code));
    }

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
