using Hermes.Notifications.Receiving.Models;

namespace Hermes.Notifications.Receiving.MailHog;

/// <summary>
/// Builds a <see cref="Uri"/> suitable as <see cref="HttpClient.BaseAddress"/> for MailHog REST calls.
/// </summary>
internal sealed class MailHogApiUriHelper
{
    /// <summary>
    /// Returns an absolute base URI ending with a slash so relative paths like <c>api/v2/messages</c> resolve correctly.
    /// </summary>
    public Uri CreateBaseUri(MailHogSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var trimmed = settings.BaseUrl.TrimEnd('/');
        return new Uri(trimmed + "/", UriKind.Absolute);
    }
}
