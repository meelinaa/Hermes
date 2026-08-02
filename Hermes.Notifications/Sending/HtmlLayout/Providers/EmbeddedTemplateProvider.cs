using System.Reflection;
using System.Text;

namespace Hermes.Notifications.Sending.HtmlLayout.Providers;

/// <summary>
/// Provides utility methods for loading embedded HTML templates from assembly manifest resources.
/// </summary>
public static class EmbeddedTemplateProvider
{
    /// <summary>
    /// Reads the content of an embedded template resource from the specified assembly by filename.
    /// </summary>
    /// <param name="assembly">The assembly containing embedded resources.</param>
    /// <param name="fileName">The target template file name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The template content as a string.</returns>
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
