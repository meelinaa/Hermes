using System.Net;
using System.Reflection;
using System.Text;

namespace Hermes.Notifications.Sending.HtmlLayout;

/// <summary>
/// Composes a full HTML newsletter from embedded templates (<c>NewsletterHeader.html</c>, <c>NewsletterItem.html</c>, <c>NewsletterFooter.html</c>).
/// </summary>
public sealed class NewsletterHtmlComposer
{
    /// <summary>
    /// Builds the complete HTML document by filling placeholders in header, repeating the item template for each article, then appending the footer.
    /// </summary>
    /// <param name="header">Header and intro text placeholders.</param>
    /// <param name="items">Article rows; empty collections produce no item rows.</param>
    /// <param name="footer">Footer links and text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UTF-8 HTML suitable for an HTML e-mail body.</returns>
    public async Task<string> BuildAsync(
        NewsletterHeaderContent header,
        IEnumerable<NewsletterItemContent> items,
        NewsletterFooterContent footer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(footer);

        var assembly = typeof(NewsletterHtmlComposer).Assembly;

        var headerTpl = await ReadEmbeddedTemplateAsync(assembly, "NewsletterHeader.html", cancellationToken).ConfigureAwait(false);
        var itemTpl = await ReadEmbeddedTemplateAsync(assembly, "NewsletterItem.html", cancellationToken).ConfigureAwait(false);
        var footerTpl = await ReadEmbeddedTemplateAsync(assembly, "NewsletterFooter.html", cancellationToken).ConfigureAwait(false);

        var headerHtml = headerTpl
            .Replace("{{HEADER}}", WebUtility.HtmlEncode(header.Header), StringComparison.Ordinal)
            .Replace("{{HEADER2}}", WebUtility.HtmlEncode(header.Header2), StringComparison.Ordinal)
            .Replace("{{DATE}}", WebUtility.HtmlEncode(header.DateDisplay), StringComparison.Ordinal)
            .Replace("{{INTRO}}", WebUtility.HtmlEncode(header.Intro), StringComparison.Ordinal);

        var itemsBuilder = new StringBuilder();
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var block = itemTpl
                .Replace("{{CATEGORY}}", WebUtility.HtmlEncode(item.Category), StringComparison.Ordinal)
                .Replace("{{TITLE}}", WebUtility.HtmlEncode(item.Title), StringComparison.Ordinal)
                .Replace("{{CONTENT}}", WebUtility.HtmlEncode(item.Content), StringComparison.Ordinal)
                .Replace("{{URL}}", WebUtility.HtmlEncode(item.Url), StringComparison.Ordinal)
                .Replace("{{IMAGEURL}}", WebUtility.HtmlEncode(item.ImageUrl), StringComparison.Ordinal);

            itemsBuilder.Append(block);
        }

        var footerHtml = footerTpl
            .Replace("{{INFOFOOTER}}", WebUtility.HtmlEncode(footer.InfoFooter), StringComparison.Ordinal)
            .Replace("{{DEABOURLFOOTER}}", WebUtility.HtmlEncode(footer.DeaboUrl), StringComparison.Ordinal)
            .Replace("{{SETTINGSFOOTER}}", WebUtility.HtmlEncode(footer.SettingsUrl), StringComparison.Ordinal);

        return string.Concat(headerHtml, itemsBuilder.ToString(), footerHtml);
    }

    private static async Task<string> ReadEmbeddedTemplateAsync( 
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
