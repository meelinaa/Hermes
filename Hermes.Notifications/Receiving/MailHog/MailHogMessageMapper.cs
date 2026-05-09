using System.Globalization;
using System.Text.Json;
using Hermes.Notifications.Receiving.DTOs;
using Hermes.Notifications.Receiving.Models;

namespace Hermes.Notifications.Receiving.MailHog;

/// <summary>
/// Maps MailHog JSON DTOs to public <see cref="EmailResult"/> instances.
/// </summary>
internal sealed class MailHogMessageMapper
{
    /// <summary>
    /// Maps a single MailHog message to an <see cref="EmailResult"/>.
    /// </summary>
    public EmailResult MapToEmailResult(MailHogMessageDto dto)
    {
        string id = dto.Id ?? string.Empty;
        string from = FormatPath(dto.From);
        string to = FormatRecipients(dto.To);
        string subject = GetHeaderValue(dto.Content?.HeadersDictionary, "Subject");
        string body = dto.Content?.Body ?? string.Empty;
        DateTimeOffset receivedAt = ParseCreated(dto.Created);
        return new EmailResult(id, from, to, subject, body, receivedAt);
    }

    private static DateTimeOffset ParseCreated(string? created)
    {
        if (string.IsNullOrWhiteSpace(created))
        {
            return default;
        }

        if (DateTimeOffset.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset dto))
        {
            return dto;
        }

        return default;
    }

    private string FormatPath(MailHogPathDto? path)
    {
        if (path is null || string.IsNullOrEmpty(path.Mailbox) || string.IsNullOrEmpty(path.Domain))
        {
            return string.Empty;
        }

        return $"{path.Mailbox}@{path.Domain}";
    }

    private string FormatRecipients(IReadOnlyList<MailHogPathDto>? paths)
    {
        if (paths is null || paths.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", paths.Select(FormatPath).Where(recipient => recipient.Length > 0));
    }

    private static string GetHeaderValue(Dictionary<string, JsonElement>? headers, string name)
    {
        if (headers is null)
        {
            return string.Empty;
        }

        foreach (KeyValuePair<string, JsonElement> pair in headers)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return ReadHeaderString(pair.Value);
            }
        }

        return string.Empty;
    }

    private static string ReadHeaderString(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() ?? string.Empty;
        }

        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
        {
            return element[0].GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}
