using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.Infrastructure.Data;

/// <summary>
/// Shared JSON options for storing complex properties in MySQL string columns.
/// </summary>
internal static class HermesJsonOptions
{
    internal static readonly JsonSerializerOptions ForEnums = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
