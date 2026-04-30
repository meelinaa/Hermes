using System.Reflection;
using System.Text;

namespace Hermes.Notifications.Sending.HtmlLayout
{
    public static class FileReaderHelper
    {
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
}
