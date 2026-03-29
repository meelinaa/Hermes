namespace Hermes.Domain.Enums;

/// <summary>
/// ISO 639-1 language code (two letters, stored lowercase for APIs such as NewsData.io).
/// </summary>
/// <remarks>
/// Initializes a new instance of <see cref="LanguageIsoCodeAttribute"/>.
/// </remarks>
/// <param name="code">ISO 639-1 code (lowercase).</param>
[AttributeUsage(AttributeTargets.Field)]
public sealed class LanguageIsoCodeAttribute(string code) : System.Attribute
{
    /// <summary>
    /// Two-letter language code (lowercase).
    /// </summary>
    public string Code { get; } = code;
}
