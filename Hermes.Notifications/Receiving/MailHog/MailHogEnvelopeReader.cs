using Hermes.Notifications.Receiving.DTOs;

namespace Hermes.Notifications.Receiving.MailHog;

/// <summary>
/// Extracts the message list from a MailHog list response, supporting both <c>items</c> and <c>messages</c> payloads.
/// </summary>
internal sealed class MailHogEnvelopeReader
{
    /// <summary>
    /// Returns the non-empty message collection from the envelope, or an empty list.
    /// </summary>
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
