using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.WebFrontend.Client.Services.NewsService;

/// <summary>Provides shared JSON serializer options for news API payloads.</summary>
public static class HermesNewsJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>Creates JSON options with enum-string conversion compatible with API payloads.</summary>
    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions o = new(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        return o;
    }
}
