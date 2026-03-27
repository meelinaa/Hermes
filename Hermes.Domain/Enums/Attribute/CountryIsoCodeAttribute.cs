
namespace Hermes.Domain.Enums.Attribute;

/// <summary>
/// ISO 3166-1 alpha-2 country code (stored lowercase for APIs such as NewsData.io).
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="CountryIsoCodeAttribute"/>.
/// </remarks>
/// <param name="code">ISO 3166-1 alpha-2 code (lowercase).</param>
[AttributeUsage(AttributeTargets.Field)]
public sealed class CountryIsoCodeAttribute(string code) : System.Attribute
{
    /// <summary>
    /// Two-letter country code (lowercase).
    /// </summary>
    public string Code { get; } = code;
}
