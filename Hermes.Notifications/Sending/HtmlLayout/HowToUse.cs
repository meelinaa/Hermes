using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hermes.Notifications.Sending.HtmlLayout
{
    internal class HowToUse
    {
        // This returns a string that contains the complete HTML for a newsletter, based on templates and provided data.
        // At that moment, this is just an example to show how to load templates and replace placeholders, but in a real application you would likely use a more robust templating engine.
        public async Task<string> BuildNewsletterAsync(string userName, IEnumerable<NewsItem> newsItems)
        {
            // Teile laden
            string header = await File.ReadAllTextAsync("Templates/newsletter_header.html");
            string itemTemplate = await File.ReadAllTextAsync("Templates/newsletter_item.html");
            string footer = await File.ReadAllTextAsync("Templates/newsletter_footer.html");

            // Header Platzhalter befüllen
            header = header
                .Replace("{{USER_NAME}}", userName)
                .Replace("{{DATE}}", DateTime.Now.ToString("dddd, dd. MMMM yyyy", new CultureInfo("de-DE")));

            // News Items in Schleife zusammenbauen
            var sb = new StringBuilder();

            foreach (var item in newsItems)
            {
                string newsBlock = itemTemplate
                    .Replace("{{CATEGORY}}", item.Category)
                    .Replace("{{TITLE}}", item.Title)
                    .Replace("{{CONTENT}}", item.Content)
                    .Replace("{{URL}}", item.Url);

                sb.Append(newsBlock);
            }

            // Zusammensetzen
            return header + sb.ToString() + footer;
        }
    }
}
