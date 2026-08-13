using Hermes.Notifications.Receiving.DTOs;

namespace Hermes.Notifications.Receiving.MailHog;

/// <summary>
/// Internal provider for extracting messages list from MailHog response envelopes.
/// </summary>
internal sealed class MailHogEnvelopeUtility
{
    /// <summary>
    /// Extracts the list of MailHog message DTOs from an envelope response.
    /// </summary>
    /// <param name="envelope">The envelope DTO received from MailHog API.</param>
    /// <returns>A read-only list of MailHog message DTOs.</returns>
    public IReadOnlyList<MailHogMessageDto> GetMessages(MailHogMessagesEnvelopeDto? envelope)
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
