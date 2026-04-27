using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.WebFrontend.Client.Services.NewsService;

public static class HermesNewsJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true));
        return o;
    }
}
