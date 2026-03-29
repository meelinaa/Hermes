using System.Reflection;
using Hermes.Domain.Enums;
using Hermes.Domain.Enums.Attribute;

namespace Hermes.Domain.Mapping;

/// <summary>
/// Maps <see cref="Country"/> to ISO 3166-1 alpha-2 codes and back using <see cref="CountryIsoCodeAttribute"/>.
/// </summary>
public static class CountryIsoCodeMapper
{
    private static readonly IReadOnlyDictionary<Country, string> ToCode = BuildForward();
    private static readonly IReadOnlyDictionary<string, Country> FromCode = BuildReverse();

    /// <summary>
    /// Returns the ISO 3166-1 alpha-2 code (lowercase) for the given country.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the enum member has no attribute.</exception>
    public static string ToIso3166Alpha2(Country country)
    {
        if (!ToCode.TryGetValue(country, out var code))
            throw new InvalidOperationException($"No ISO 3166-1 code defined for {country}.");
        
        return code;
    }

    /// <summary>
    /// Resolves a country from an ISO 3166-1 alpha-2 code (comparison is case-insensitive).
    /// </summary>
    public static bool TryGetCountry(string iso3166Alpha2, out Country country)
    {
        country = default;
        if (string.IsNullOrWhiteSpace(iso3166Alpha2))
            return false;
        
        var normalized = iso3166Alpha2.Trim();
        if (normalized.Length != 2)
            return false;
       
        return FromCode.TryGetValue(normalized.ToLowerInvariant(), out country);
    }

    /// <summary>
    /// Resolves a country from an ISO 3166-1 alpha-2 code or throws if unknown.
    /// </summary>
    public static Country ParseCountry(string iso3166Alpha2)
    {
        if (TryGetCountry(iso3166Alpha2, out var country))
            return country;
        
        throw new ArgumentException($"Unknown ISO 3166-1 alpha-2 code: {iso3166Alpha2}", nameof(iso3166Alpha2));
    }

    private static Dictionary<Country, string> BuildForward()
    {
        var map = new Dictionary<Country, string>();
        foreach (var value in Enum.GetValues<Country>())
        {
            var field = typeof(Country).GetField(value.ToString());
            var attr = field?.GetCustomAttribute<CountryIsoCodeAttribute>();
            if (attr is null)
                throw new InvalidOperationException($"Country.{value} is missing [{nameof(CountryIsoCodeAttribute)}].");
            
            map[value] = attr.Code;
        }

        return map;
    }

    private static Dictionary<string, Country> BuildReverse()
    {
        var map = new Dictionary<string, Country>(StringComparer.Ordinal);
        foreach (var kv in ToCode)
        {
            if (map.TryGetValue(kv.Value, out Country value))
                throw new InvalidOperationException($"Duplicate ISO 3166-1 code '{kv.Value}' for {kv.Key} and {value}.");
            
            map[kv.Value] = kv.Key;
        }

        return map;
    }
}
