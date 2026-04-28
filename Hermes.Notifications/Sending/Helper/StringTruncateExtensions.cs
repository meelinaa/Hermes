namespace Hermes.Notifications.Sending.Helper;

internal static class StringTruncateExtensions
{
    public static string Truncate(this string? value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;
        return string.Concat(value.AsSpan(0, maxLength - suffix.Length), suffix);
    }
}
