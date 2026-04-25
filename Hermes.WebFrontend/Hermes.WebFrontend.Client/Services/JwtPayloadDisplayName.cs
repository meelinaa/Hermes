using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.WebFrontend.Client.Services;

/// <summary>
/// Reads claims from JWT payload (signed token; no validation — client display / routing only).
/// </summary>
public static class JwtPayloadDisplayName
{
    private const string ClaimName =
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

    private const string ClaimEmail =
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

    public static string? TryGet(string? accessToken)
    {
        var json = DecodePayloadJson(accessToken);
        if (json is null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var o = doc.RootElement;
            if (TryString(o, ClaimName, out var name))
                return name;
            if (TryString(o, "name", out name))
                return name;
            if (TryString(o, "unique_name", out name))
                return name;
            if (TryString(o, ClaimEmail, out var email))
                return email;
            if (TryString(o, "email", out email))
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
        var json = DecodePayloadJson(accessToken);
        if (json is null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var o = doc.RootElement;
            if (!o.TryGetProperty("sub", out var sub))
                return null;
            if (sub.ValueKind == JsonValueKind.String)
            {
                if (int.TryParse(sub.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
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
        var json = DecodePayloadJson(accessToken);
        if (json is null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var o = doc.RootElement;
            if (!o.TryGetProperty("exp", out var exp))
                return null;
            if (exp.ValueKind == JsonValueKind.Number)
                return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            if (exp.ValueKind == JsonValueKind.String)
            {
                if (long.TryParse(exp.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sec))
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
        var parts = accessToken.Split('.', StringSplitOptions.RemoveEmptyEntries);
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

    private static bool TryString(JsonElement o, string property, out string? value)
    {
        value = null;
        if (!o.TryGetProperty(property, out var el))
            return false;
        if (el.ValueKind != JsonValueKind.String)
            return false;
        value = el.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }

        return Convert.FromBase64String(s);
    }
}
