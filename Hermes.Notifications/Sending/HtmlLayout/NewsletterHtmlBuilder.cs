using Hermes.Notifications.Sending.HtmlLayout.Models;
using System.Net;
using System.Reflection;
using System.Text;

namespace Hermes.Notifications.Sending.HtmlLayout;

public sealed class NewsletterHtmlBuilder
{
    public static async Task<string> BuildAsync(
        NewsletterHeaderContentDto header,
        IEnumerable<NewsletterItemContentDto> items,
        NewsletterFooterContentDto footer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(footer);

        Assembly assembly = typeof(NewsletterHtmlBuilder).Assembly;

        string? headerTpl = await EmbeddedTemplateProvider.ReadEmbeddedTemplateAsync(assembly, "NewsletterHeader.html", cancellationToken).ConfigureAwait(false);
        string? itemTpl = await EmbeddedTemplateProvider.ReadEmbeddedTemplateAsync(assembly, "NewsletterItem.html", cancellationToken).ConfigureAwait(false);
        string? footerTpl = await EmbeddedTemplateProvider.ReadEmbeddedTemplateAsync(assembly, "NewsletterFooter.html", cancellationToken).ConfigureAwait(false);

        string? headerHtml = headerTpl
            .Replace("{{HEADER}}", WebUtility.HtmlEncode(header.Header), StringComparison.Ordinal)
            .Replace("{{HEADER2}}", WebUtility.HtmlEncode(header.Header2), StringComparison.Ordinal)
            .Replace("{{DATE}}", WebUtility.HtmlEncode(header.DateDisplay), StringComparison.Ordinal)
            .Replace("{{INTRO}}", WebUtility.HtmlEncode(header.Intro), StringComparison.Ordinal);

        StringBuilder itemsBuilder = new();
        foreach (NewsletterItemContentDto item in items)
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
