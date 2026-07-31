using Hermes.Notifications.Receiving.Models;

namespace Hermes.Notifications.Receiving.MailHog;

internal sealed class MailHogApiUriFactory
{
    public static Uri CreateBaseUri(MailHogOptions settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? trimmed = settings.BaseUrl.TrimEnd('/');
        return new Uri(trimmed + "/", UriKind.Absolute);
    }
}
