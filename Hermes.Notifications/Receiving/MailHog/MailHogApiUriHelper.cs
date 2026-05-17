using Hermes.Notifications.Receiving.Models;

namespace Hermes.Notifications.Receiving.MailHog;

internal sealed class MailHogApiUriHelper
{
    public static Uri CreateBaseUri(MailHogSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? trimmed = settings.BaseUrl.TrimEnd('/');
        return new Uri(trimmed + "/", UriKind.Absolute);
    }
}
