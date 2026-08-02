using Hermes.Notifications.Receiving.Options;

namespace Hermes.Notifications.Receiving.MailHog;

/// <summary>
/// Internal factory for constructing normalized MailHog API URIs.
/// </summary>
internal sealed class MailHogApiUriFactory
{
    /// <summary>
    /// Creates a normalized base URI from MailHog options.
    /// </summary>
    /// <param name="settings">The MailHog configuration settings.</param>
    /// <returns>A normalized base URI with trailing slash.</returns>
    public static Uri CreateBaseUri(MailHogOptions settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? trimmed = settings.BaseUrl.TrimEnd('/');
        return new Uri(trimmed + "/", UriKind.Absolute);
    }
}
