namespace Hermes.Domain.Enums;

[AttributeUsage(AttributeTargets.Field)]
public sealed class LanguageIsoCodeAttribute(string code) : System.Attribute
{
    public string Code { get; } = code;
}
