using System.Globalization;
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
/// Service responsible for composing and delivering personalized news digest emails to subscribers.
/// Follows ISP by depending on <see cref="IUserStore"/> for user profile lookup.
/// </summary>
public sealed class NewsletterDigestService(
    IUserStore users,
    INewsletterSubscriptionRepository newsletterSubscriptions,
    IArticleFetchingService articleFetchingService,
    IEmailProvider emailSender,
    INewsletterHtmlService newsletterRenderer,
    TimeProvider timeProvider) : INewsletterDigestService
{
    private const int MAX_ARTICLES_IN_NEWSLETTER = 5;
    private readonly CultureInfo _digestCulture = new("de-DE");

    /// <summary>
    /// Fetches matching news articles for a user subscription, renders HTML markup, and sends the digest email.
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

        string? subject = $"Hermes Newsletter (#{newsId.Value}) — {timeProvider.GetUtcNow().UtcDateTime.ToString("d", _digestCulture)}";

        List<NewsletterArticleItemDto> articleItems = articles
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
