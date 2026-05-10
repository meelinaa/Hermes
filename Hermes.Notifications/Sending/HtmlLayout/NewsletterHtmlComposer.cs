using Hermes.Notifications.Sending.HtmlLayout.Models;
using System.Net;
using System.Reflection;
using System.Text;

namespace Hermes.Notifications.Sending.HtmlLayout;

public sealed class NewsletterHtmlComposer
{
    public static async Task<string> BuildAsync(
        NewsletterHeaderContent header,
        IEnumerable<NewsletterItemContent> items,
        NewsletterFooterContent footer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(footer);

        Assembly assembly = typeof(NewsletterHtmlComposer).Assembly;

        string? headerTpl = await FileReaderHelper.ReadEmbeddedTemplateAsync(assembly, "NewsletterHeader.html", cancellationToken).ConfigureAwait(false);
        string? itemTpl = await FileReaderHelper.ReadEmbeddedTemplateAsync(assembly, "NewsletterItem.html", cancellationToken).ConfigureAwait(false);
        string? footerTpl = await FileReaderHelper.ReadEmbeddedTemplateAsync(assembly, "NewsletterFooter.html", cancellationToken).ConfigureAwait(false);

        string? headerHtml = headerTpl
            .Replace("{{HEADER}}", WebUtility.HtmlEncode(header.Header), StringComparison.Ordinal)
            .Replace("{{HEADER2}}", WebUtility.HtmlEncode(header.Header2), StringComparison.Ordinal)
            .Replace("{{DATE}}", WebUtility.HtmlEncode(header.DateDisplay), StringComparison.Ordinal)
            .Replace("{{INTRO}}", WebUtility.HtmlEncode(header.Intro), StringComparison.Ordinal);

        StringBuilder itemsBuilder = new();
        foreach (NewsletterItemContent item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? block = itemTpl
                .Replace("{{CATEGORY}}", WebUtility.HtmlEncode(item.Category), StringComparison.Ordinal)
                .Replace("{{TITLE}}", WebUtility.HtmlEncode(item.Title), StringComparison.Ordinal)
                .Replace("{{CONTENT}}", WebUtility.HtmlEncode(item.Content), StringComparison.Ordinal)
                .Replace("{{URL}}", WebUtility.HtmlEncode(item.Url), StringComparison.Ordinal)
                .Replace("{{IMAGEURL}}", WebUtility.HtmlEncode(item.ImageUrl), StringComparison.Ordinal);

            itemsBuilder.Append(block);
        }

        string? footerHtml = footerTpl
            .Replace("{{INFOFOOTER}}", WebUtility.HtmlEncode(footer.InfoFooter), StringComparison.Ordinal)
            .Replace("{{DEABOURLFOOTER}}", WebUtility.HtmlEncode(footer.DeaboUrl), StringComparison.Ordinal)
            .Replace("{{SETTINGSFOOTER}}", WebUtility.HtmlEncode(footer.SettingsUrl), StringComparison.Ordinal);

        return string.Concat(headerHtml, itemsBuilder.ToString(), footerHtml);
    }
}
