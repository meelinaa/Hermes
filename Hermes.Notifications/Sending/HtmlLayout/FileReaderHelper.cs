using System.Reflection;
using System.Text;

namespace Hermes.Notifications.Sending.HtmlLayout;

/// <summary>
/// Loads embedded resources from an assembly by matching manifest resource names that end with a given file name (case-insensitive).
/// </summary>
public static class FileReaderHelper
{
    /// <summary>
    /// Reads the full UTF-8 text of an embedded resource whose manifest name ends with <paramref name="fileName"/>.
    /// </summary>
    /// <param name="assembly">Assembly that contains the embedded resource (e.g. <c>typeof(NewsletterHtmlComposer).Assembly</c>).</param>
    /// <param name="fileName">Suffix to match against manifest resource names, such as <c>NewsletterHeader.html</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The template file contents.</returns>
    /// <exception cref="InvalidOperationException">No matching embedded resource exists, or the resource stream could not be opened.</exception>
    public static async Task<string> ReadEmbeddedTemplateAsync(
        Assembly assembly,
        string fileName,
        CancellationToken cancellationToken)
    {
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException(
                $"Embedded resource ending with '{fileName}' was not found. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
        }

        await using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Could not open embedded resource '{resourceName}'.");
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
