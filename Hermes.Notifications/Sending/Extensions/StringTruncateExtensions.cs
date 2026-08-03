namespace Hermes.Notifications.Sending.Extensions;

/// <summary>
/// Extension methods for string truncation utilities.
/// </summary>
internal static class StringTruncateExtensions
{
    /// <summary>
    /// Truncates a string to a specified maximum length, appending a suffix if truncated.
    /// </summary>
    /// <param name="value">The string value to truncate.</param>
    /// <param name="maxLength">The maximum allowed length.</param>
    /// <param name="suffix">The suffix to append if truncated (defaults to "...").</param>
    /// <returns>The truncated string.</returns>
    public static string Truncate(this string? value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Length <= maxLength)
            return value;
        return string.Concat(value.AsSpan(0, maxLength - suffix.Length), suffix);
    }
}
