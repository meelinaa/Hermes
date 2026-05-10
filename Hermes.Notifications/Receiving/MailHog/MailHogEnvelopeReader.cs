using Hermes.Notifications.Receiving.DTOs;

namespace Hermes.Notifications.Receiving.MailHog;

internal sealed class MailHogEnvelopeReader
{
    public IReadOnlyList<MailHogMessageDto> GetMessages(MailHogMessagesEnvelope? envelope)
    {
        if (envelope is null)
        {
            return [];
        }

        if (envelope.Items is { Count: > 0 })
        {
            return envelope.Items;
        }

        if (envelope.Messages is { Count: > 0 })
        {
            return envelope.Messages;
        }

        return [];
    }
}
