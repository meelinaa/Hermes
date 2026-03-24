using System.Text.Json.Serialization;

namespace Hermes.Notifications.Receiving.DTOs
{
    internal sealed class MailHogMessagesEnvelope
    {
        [JsonPropertyName("items")]
        public List<MailHogMessageDto>? Items { get; init; }

        [JsonPropertyName("messages")]
        public List<MailHogMessageDto>? Messages { get; init; }
    }
}
