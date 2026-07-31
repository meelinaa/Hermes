using System.Reflection;
using System.Text;

namespace Hermes.Notifications.Sending.HtmlLayout;

public static class EmbeddedTemplateProvider
{
    public static async Task<string> ReadEmbeddedTemplateAsync(
        Assembly assembly,
        string fileName,
        CancellationToken cancellationToken)
    {
        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(resource => resource.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException(
                $"Embedded resource ending with '{fileName}' was not found. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        await using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Could not open embedded resource '{resourceName}'.");
        }

        using StreamReader reader = new(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
