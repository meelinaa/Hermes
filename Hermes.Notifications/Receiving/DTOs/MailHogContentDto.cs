using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.Notifications.Receiving.DTOs
{
    internal sealed class MailHogContentDto
    {
        [JsonPropertyName("Headers")]
        public Dictionary<string, JsonElement>? HeadersDictionary { get; init; }

        public string? Body { get; init; }
    }
}
