using System.Reflection;
using Hermes.Domain.Enums;
using Hermes.Domain.Enums.Attributes;

namespace Hermes.Application.Mapping;

public static class CountryIsoCodeMapper
{
    private static readonly IReadOnlyDictionary<Country, string> _toCode = BuildForward();
    private static readonly IReadOnlyDictionary<string, Country> _fromCode = BuildReverse();

    public static string ToIso3166Alpha2(Country country)
    {
        if (!_toCode.TryGetValue(country, out string? code))
            throw new InvalidOperationException($"No ISO 3166-1 code defined for {country}.");

        return code;
    }

    public static bool TryGetCountry(string iso3166Alpha2, out Country country)
    {
        country = default;
        if (string.IsNullOrWhiteSpace(iso3166Alpha2))
            return false;

        string? normalized = iso3166Alpha2.Trim();
        if (normalized.Length != 2)
            return false;

        return _fromCode.TryGetValue(normalized.ToLowerInvariant(), out country);
    }

    public static Country ParseCountry(string iso3166Alpha2)
    {
        if (TryGetCountry(iso3166Alpha2, out Country country))
            return country;

        throw new ArgumentException($"Unknown ISO 3166-1 alpha-2 code: {iso3166Alpha2}", nameof(iso3166Alpha2));
    }

    private static Dictionary<Country, string> BuildForward()
    {
        Dictionary<Country, string> map = [];
        foreach (Country value in Enum.GetValues<Country>())
        {
            FieldInfo? field = typeof(Country).GetField(value.ToString());
            CountryIsoCodeAttribute? attr = field?.GetCustomAttribute<CountryIsoCodeAttribute>();
            if (attr is null)
                throw new InvalidOperationException($"Country.{value} is missing [{nameof(CountryIsoCodeAttribute)}].");

            map[value] = attr.Code;
        }

        return map;
    }

    private static Dictionary<string, Country> BuildReverse()
    {
        Dictionary<string, Country> map = new(StringComparer.Ordinal);
        foreach (KeyValuePair<Country, string> kv in _toCode)
        {
            if (map.TryGetValue(kv.Value, out Country value))
                throw new InvalidOperationException($"Duplicate ISO 3166-1 code '{kv.Value}' for {kv.Key} and {value}.");

            map[kv.Value] = kv.Key;
        }

        return map;
    }
}
