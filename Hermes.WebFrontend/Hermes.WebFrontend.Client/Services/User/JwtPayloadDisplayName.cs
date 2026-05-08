using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.WebFrontend.Client.Services.User;

/// <summary>
/// Reads claims from JWT payload (signed token; no validation — client display / routing only).
/// </summary>
public static class JwtPayloadDisplayName
{
    private const string CLAIM_NAME =
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

    private const string CLAIM_EMAIL =
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

    public static string? TryGet(string? accessToken)
    {
        string? json = DecodePayloadJson(accessToken);
        if (json is null)
            return null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement element = doc.RootElement;
            if (TryString(element, CLAIM_NAME, out string? name))
                return name;
            if (TryString(element, "name", out name))
                return name;
            if (TryString(element, "unique_name", out name))
                return name;
            if (TryString(element, CLAIM_EMAIL, out string? email))
                return email;
            if (TryString(element, "email", out email))
                return email;
        }
        catch
        {
            // ignore
        }

        return null;
    }

    /// <summary>Returns <c>sub</c> claim as user id (matches API JWT).</summary>
    public static int? TryGetUserId(string? accessToken)
    {
        string? json = DecodePayloadJson(accessToken);
        if (json is null)
            return null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement element = doc.RootElement;
            if (!element.TryGetProperty("sub", out JsonElement sub))
                return null;
            if (sub.ValueKind == JsonValueKind.String)
            {
                if (int.TryParse(sub.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                    return id;
            }
            else if (sub.ValueKind == JsonValueKind.Number)
            {
                return sub.GetInt32();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    /// <summary>JWT <c>exp</c> as UTC (Unix seconds); no signature validation.</summary>
    public static DateTimeOffset? TryGetExpiresAtUtc(string? accessToken)
    {
        string? json = DecodePayloadJson(accessToken);
        if (json is null)
            return null;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement element = doc.RootElement;
            if (!element.TryGetProperty("exp", out JsonElement exp))
                return null;
            if (exp.ValueKind == JsonValueKind.Number)
                return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            if (exp.ValueKind == JsonValueKind.String)
            {
                if (long.TryParse(exp.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long sec))
                    return DateTimeOffset.FromUnixTimeSeconds(sec);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? DecodePayloadJson(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;
        string[] parts = accessToken.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;
        try
        {
            return Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        }
        catch
        {
            return null;
        }
    }

    private static bool TryString(JsonElement payloadElement, string property, out string? value)
    {
        value = null;
        if (!payloadElement.TryGetProperty(property, out JsonElement propertyElement))
            return false;
        if (propertyElement.ValueKind != JsonValueKind.String)
            return false;
        value = propertyElement.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        string normalized = input.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2: normalized += "=="; break;
            case 3: normalized += "="; break;
        }

        return Convert.FromBase64String(normalized);
    }
}
