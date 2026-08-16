using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using FluentResults;
using Hermes.Application.DTOs.Email;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Mapping;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services.Newsletter;

/// <summary>
/// Service responsible for composing, deduplicating, and delivering personalized news digest emails to subscribers.
/// Follows ISP by depending on <see cref="IUserStore"/> for user profile lookup.
/// </summary>
public sealed class NewsletterDigestService(
    IUserStore users,
    INewsletterSubscriptionStore newsletterSubscriptions,
    IArticleFetchingService articleFetchingService,
    IEmailProvider emailSender,
    INewsletterHtmlService newsletterRenderer,
    TimeProvider timeProvider) : INewsletterDigestService
{
    private const int MAX_ARTICLES_IN_NEWSLETTER = 5;
    private const int MIN_TITLE_LENGTH_FOR_DEDUP = 25;
    private readonly CultureInfo _digestCulture = new("de-DE");

    private static readonly HashSet<string> IgnoredQueryParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
        "fbclid", "gclid", "ref", "source", "ncid", "ocid", "cmpid"
    };

    private static readonly Regex PunctuationRegex = new(@"[^\w\s]", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Fetches matching news articles for a user subscription, deduplicates entries across URLs and specific titles, renders HTML markup, and sends the digest email.
    /// </summary>
    /// <param name="userId">The unique identifier of the recipient user.</param>
    /// <param name="newsId">The unique identifier of the newsletter subscription.</param>
    /// <param name="digestSlotStartUtc">The UTC schedule slot timestamp.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A Result containing true if an email was sent, or false if skipped.</returns>
    public async Task<Result<bool>> SendAsync(UserId userId, NewsletterId newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            return Result.Fail("User ID must be positive.");
        if (newsId.Value <= 0)
            return Result.Fail("News ID must be positive.");

        User? user = await users.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email.Value))
            return Result.Ok(false);

        NewsletterSubscription? subscription = await newsletterSubscriptions.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsEnabled)
            return Result.Ok(false);

        IReadOnlyList<NewsArticle> articles = await articleFetchingService.FetchArticlesForSubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);
        if (articles.Count == 0)
            return Result.Ok(false);

        IReadOnlyList<NewsArticle> deduplicatedArticles = DeduplicateArticles(articles);
        if (deduplicatedArticles.Count == 0)
            return Result.Ok(false);

        string? subject = $"Hermes Newsletter (#{newsId.Value}) — {timeProvider.GetUtcNow().UtcDateTime.ToString("d", _digestCulture)}";

        List<NewsletterArticleItemDto> articleItems = deduplicatedArticles
            .Take(MAX_ARTICLES_IN_NEWSLETTER)
            .Select(a => new NewsletterArticleItemDto(
                Category: a.Category?.FirstOrDefault() ?? "News",
                Title: a.Title ?? string.Empty,
                Content: TruncatePlainText(a.Description, 150),
                Url: a.Link ?? "#",
                ImageUrl: a.ImageUrl ?? string.Empty))
            .ToList();

        NewsletterRenderRequestDto renderRequest = new(user.Name, articleItems);
        string body = await newsletterRenderer
            .RenderNewsletterAsync(renderRequest, cancellationToken)
            .ConfigureAwait(false);

        await emailSender.SendAsync(
            new EmailMessageDto(
                new EmailRecipientDto(user.Email!.Value.Trim(), string.IsNullOrWhiteSpace(user.Name) ? null : user.Name),
                subject,
                body),
            cancellationToken).ConfigureAwait(false);

        return Result.Ok(true);
    }

    /// <summary>
    /// Deduplicates a raw collection of news articles using a multi-stage heuristic:
    /// Stage 1: Strips tracking query parameters and canonicalizes URLs.
    /// Stage 2: Deduplicates identical normalized titles for long/specific headlines without false-colliding generic titles.
    /// </summary>
    /// <param name="articles">The raw collection of articles.</param>
    /// <returns>A deduplicated list of news articles preserving initial ordering.</returns>
    public static IReadOnlyList<NewsArticle> DeduplicateArticles(IEnumerable<NewsArticle> articles)
    {
        if (articles is null)
            return [];

        List<NewsArticle> result = [];
        HashSet<string> seenUrls = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenSpecificTitles = new(StringComparer.OrdinalIgnoreCase);

        foreach (NewsArticle article in articles)
        {
            if (article is null)
                continue;

            string normalizedUrl = NormalizeUrl(article.Link);
            if (!string.IsNullOrWhiteSpace(normalizedUrl))
            {
                if (!seenUrls.Add(normalizedUrl))
                    continue; // Duplicate URL
            }

            string normalizedTitle = NormalizeTitle(article.Title);
            if (!string.IsNullOrWhiteSpace(normalizedTitle) && normalizedTitle.Length >= MIN_TITLE_LENGTH_FOR_DEDUP)
            {
                string domainKey = GetDomainKey(article.Link);
                string dedupKey = string.IsNullOrEmpty(domainKey) ? normalizedTitle : $"{domainKey}::{normalizedTitle}";

                if (!seenSpecificTitles.Add(dedupKey))
                    continue; // Duplicate title from same or generic domain
            }

            result.Add(article);
        }

        return result;
    }

    /// <summary>
    /// Normalizes a target URL by removing tracking query parameters (e.g. utm_*, fbclid), lowercase scheme/host, and trimming trailing slashes.
    /// </summary>
    /// <param name="rawUrl">The raw URL string.</param>
    /// <returns>A normalized canonical URL string.</returns>
    public static string NormalizeUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return string.Empty;

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out Uri? parsedUri))
            return rawUrl.Trim().TrimEnd('/');

        string scheme = parsedUri.Scheme.ToLowerInvariant();
        string host = parsedUri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            host = host[4..];

        string path = parsedUri.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrEmpty(path))
            path = "/";

        var queryParams = HttpUtility.ParseQueryString(parsedUri.Query);
        List<string> cleanParams = [];
        foreach (string? key in queryParams.AllKeys)
        {
            if (string.IsNullOrWhiteSpace(key) || IgnoredQueryParams.Contains(key))
                continue;

            string[]? values = queryParams.GetValues(key);
            if (values != null)
            {
                foreach (string val in values)
                {
                    cleanParams.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(val)}");
                }
            }
        }

        cleanParams.Sort(StringComparer.Ordinal);
        string queryString = cleanParams.Count > 0 ? "?" + string.Join("&", cleanParams) : string.Empty;

        return $"{scheme}://{host}{path}{queryString}";
    }

    /// <summary>
    /// Normalizes a headline title by lowercasing, stripping punctuation, and collapsing multiple whitespaces.
    /// </summary>
    /// <param name="rawTitle">The raw headline string.</param>
    /// <returns>A normalized headline string.</returns>
    public static string NormalizeTitle(string? rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return string.Empty;

        string withoutPunctuation = PunctuationRegex.Replace(rawTitle.ToLowerInvariant(), " ");
        return WhitespaceRegex.Replace(withoutPunctuation, " ").Trim();
    }

    /// <summary>
    /// Extracts the root domain name from a URL for domain-scoped deduplication.
    /// </summary>
    /// <param name="rawUrl">The raw URL string.</param>
    /// <returns>The extracted host name or empty string.</returns>
    private static string GetDomainKey(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return string.Empty;

        if (Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out Uri? uri))
        {
            string host = uri.Host.ToLowerInvariant();
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        }

        return string.Empty;
    }

    /// <summary>
    /// Truncates plain text content to the specified maximum length and appends an ellipsis suffix.
    /// </summary>
    /// <param name="value">The raw string to truncate.</param>
    /// <param name="maxLength">The maximum allowed character length.</param>
    /// <param name="suffix">The suffix to append upon truncation.</param>
    /// <returns>A truncated string or empty string if null.</returns>
    private static string TruncatePlainText(string? value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Length <= maxLength)
            return value;
        return string.Concat(value.AsSpan(0, maxLength - suffix.Length), suffix);
    }
}
