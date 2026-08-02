namespace Hermes.Domain.Enums.Attributes;

/// <summary>
/// Custom attribute associating an ISO 3166-1 alpha-2 country code with a <see cref="Country"/> enum member.
/// </summary>
/// <param name="code">The ISO 3166-1 alpha-2 country code.</param>
[AttributeUsage(AttributeTargets.Field)]
public sealed class CountryIsoCodeAttribute(string code) : Attribute
{
    /// <summary>
    /// Gets the ISO 3166-1 alpha-2 country code string.
    /// </summary>
    public string Code { get; } = code;
}
