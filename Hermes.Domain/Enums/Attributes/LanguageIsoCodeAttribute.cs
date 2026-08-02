namespace Hermes.Domain.Enums.Attributes;

/// <summary>
/// Custom attribute associating an ISO 639-1 language code with a <see cref="Language"/> enum member.
/// </summary>
/// <param name="code">The ISO 639-1 language code.</param>
[AttributeUsage(AttributeTargets.Field)]
public sealed class LanguageIsoCodeAttribute(string code) : Attribute
{
    /// <summary>
    /// Gets the ISO 639-1 language code string.
    /// </summary>
    public string Code { get; } = code;
}
