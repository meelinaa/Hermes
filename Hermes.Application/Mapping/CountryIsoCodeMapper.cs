using System.Collections.Frozen;
using System.Reflection;
using Hermes.Domain.Enums;
using Hermes.Domain.Enums.Attributes;

namespace Hermes.Application.Mapping;

/// <summary>
/// High-performance bidirectional mapper between <see cref="Country"/> enum values and ISO 3166-1 alpha-2 country codes.
/// Uses immutable <see cref="FrozenDictionary{TKey, TValue}"/> for zero-allocation, O(1) hash lookups.
/// </summary>
public static class CountryIsoCodeMapper
{
    private static readonly FrozenDictionary<Country, string> _toCode = BuildForward();
    private static readonly FrozenDictionary<string, Country> _fromCode = BuildReverse();

    /// <summary>
    /// Converts a <see cref="Country"/> enum value to its corresponding ISO 3166-1 alpha-2 two-letter lowercase string code.
    /// </summary>
    /// <param name="country">The domain country enum value.</param>
    /// <returns>The ISO 3166-1 alpha-2 code (e.g. "de", "us").</returns>
    public static string ToIso3166Alpha2(Country country)
    {
        if (!_toCode.TryGetValue(country, out string? code))
            throw new InvalidOperationException($"No ISO 3166-1 code defined for {country}.");

        return code;
    }

    /// <summary>
    /// Attempts to parse an ISO 3166-1 alpha-2 string code into a <see cref="Country"/> enum value without memory allocation.
    /// Case-insensitive matching via FrozenDictionary.
    /// </summary>
    /// <param name="iso3166Alpha2">The ISO 3166-1 alpha-2 country code.</param>
    /// <param name="country">When this method returns, contains the matching <see cref="Country"/> enum value.</param>
    /// <returns>True if the country code was recognized; otherwise false.</returns>
    public static bool TryGetCountry(string? iso3166Alpha2, out Country country)
    {
        country = default;
        if (string.IsNullOrWhiteSpace(iso3166Alpha2))
            return false;

        string normalized = iso3166Alpha2.Trim();
        if (normalized.Length != 2)
            return false;

        return _fromCode.TryGetValue(normalized, out country);
    }

    /// <summary>
    /// Parses an ISO 3166-1 alpha-2 string code into a <see cref="Country"/> enum value or throws an exception.
    /// </summary>
    /// <param name="iso3166Alpha2">The ISO 3166-1 alpha-2 country code.</param>
    /// <returns>The matching <see cref="Country"/> enum value.</returns>
    public static Country ParseCountry(string iso3166Alpha2)
    {
        if (TryGetCountry(iso3166Alpha2, out Country country))
            return country;

        throw new ArgumentException($"Unknown ISO 3166-1 alpha-2 code: {iso3166Alpha2}", nameof(iso3166Alpha2));
    }

    /// <summary>
    /// Builds the forward mapping from Country enum values to ISO 3166-1 alpha-2 codes via reflection.
    /// </summary>
    /// <returns>A frozen dictionary of Country to ISO code.</returns>
    private static FrozenDictionary<Country, string> BuildForward()
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

        return map.ToFrozenDictionary();
    }

    /// <summary>
    /// Builds the reverse mapping from ISO 3166-1 alpha-2 codes to Country enum values with case-insensitive comparisons.
    /// </summary>
    /// <returns>A frozen dictionary of ISO code to Country.</returns>
    private static FrozenDictionary<string, Country> BuildReverse()
    {
        Dictionary<string, Country> map = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<Country, string> kv in _toCode)
        {
            if (map.TryGetValue(kv.Value, out Country value))
                throw new InvalidOperationException($"Duplicate ISO 3166-1 code '{kv.Value}' for {kv.Key} and {value}.");

            map[kv.Value] = kv.Key;
        }

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
