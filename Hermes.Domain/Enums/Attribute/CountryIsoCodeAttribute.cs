
namespace Hermes.Domain.Enums.Attribute;

[AttributeUsage(AttributeTargets.Field)]
public sealed class CountryIsoCodeAttribute(string code) : System.Attribute
{
    public string Code { get; } = code;
}
